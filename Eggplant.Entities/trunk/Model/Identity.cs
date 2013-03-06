using System;
using System.Linq;
using System.Collections.Generic;

namespace Eggplant.Entities.Model
{
	public class IdentityPartDefinition
	{
		public readonly IEntityProperty Property;

		public IdentityPartDefinition(IEntityProperty property)
		{
			Property = property;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IdentityPartDefinition))
				return false;
			IdentityPartDefinition otherSegment = (IdentityPartDefinition)obj;

			return
				this.Property == otherSegment.Property;
		}

		public override int GetHashCode()
		{
			return Property.GetHashCode();
		}

		public override string ToString()
		{
			return Property.Name;
		}
	}

	public class IdentityDefinition
	{
		public readonly IdentityPartDefinition[] PartDefinitions;
		public Dictionary<IEntityProperty, Func<object, bool>> Constraints = null;
		private int _hash;

		public IdentityDefinition(params IEntityProperty[] parts)
		{
			if (parts == null || parts.Length < 1)
				throw new ArgumentException("One or more properties are required.", "parts");

			this.PartDefinitions = new IdentityPartDefinition[parts.Length];
			for (int i = 0; i < parts.Length; i++)
				this.PartDefinitions[i] = new IdentityPartDefinition(parts[i]);

			_hash = HashingHelper.Hash(this.PartDefinitions);
		}

		public IdentityDefinition Constrain<T>(IEntityProperty<T> property, Func<T, bool> checkIfValid)
		{
			if (this.Constraints == null)
				this.Constraints = new Dictionary<IEntityProperty, Func<object, bool>>();

			this.Constraints.Add(property, obj => checkIfValid((T)obj));

			return this;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IdentityDefinition))
				return false;

			IdentityDefinition defToCompare = (IdentityDefinition)obj;

			// Part length must match
			if (defToCompare.PartDefinitions.Length != this.PartDefinitions.Length)
				return false;

			// Compare the order of the parts and their values
			for (int i = 0; i < this.PartDefinitions.Length; i++)
			{
				if (!Object.Equals(this.PartDefinitions[i], defToCompare.PartDefinitions[i]))
					return false;
			}

			return true;
		}

		public override int GetHashCode()
		{
			return _hash;
		}

		public override string ToString()
		{
			string output = String.Empty;
			for (int i = 0; i < PartDefinitions.Length; i++)
			{
				output += PartDefinitions[i].ToString();
				if (i < PartDefinitions.Length - 1)
					output += ",";
			}
			return output;
		}
		
		public Identity IdentityOf(object obj)
		{
			IdentityPart[] parts = new IdentityPart[this.PartDefinitions.Length];
			for (int i = 0; i < parts.Length; i++)
			{
				parts[i] = new IdentityPart(this.PartDefinitions[i], this.PartDefinitions[i].Property.GetValue(obj));
			}

			ValidateConstraints(obj);

			return new Identity(this, parts);
		}

		public Identity IdentityFromValues(IDictionary<IEntityProperty, object> propertyValues)
		{
			IdentityPart[] parts = new IdentityPart[this.PartDefinitions.Length];
			try
			{
				for (int i = 0; i < parts.Length; i++)
				{
					parts[i] = new IdentityPart(this.PartDefinitions[i], propertyValues[this.PartDefinitions[i].Property]);
				}
			}
			catch (KeyNotFoundException)
			{
				throw new KeyNotFoundException(String.Format("The identity {{{0}}} requires properties that are not available.", this));
			}

			ValidateConstraints(propertyValues);

			return new Identity(this, parts);

		}

		public Identity IdentityFromValues(params object[] values)
		{
			if (values.Length != this.PartDefinitions.Length)
				throw new ArgumentException("The number of values is different than the identity definition parts.", "values");

			IdentityPart[] parts = new IdentityPart[this.PartDefinitions.Length];
			for (int i = 0; i < parts.Length; i++)
			{
				if (!this.PartDefinitions[i].Property.PropertyType.IsAssignableFrom(values[i].GetType()))
					throw new ArgumentException("Passed value types don't match the parts of the identity definition.");

				parts[i] = new IdentityPart(this.PartDefinitions[i], values[i]);
			}

			return new Identity(this, parts);
		}

		public bool HasValidValues(IDictionary<IEntityProperty, object> propertyValues)
		{
			for (int i = 0; i < this.PartDefinitions.Length; i++)
			{
				if (!propertyValues.ContainsKey(this.PartDefinitions[i].Property))
					return false;
			}

			return ValidateConstraints(propertyValues, false);
		}

		public bool HasValidValues(params object[] values)
		{
			if (values.Length != this.PartDefinitions.Length)
				return false;

			IdentityPart[] parts = new IdentityPart[this.PartDefinitions.Length];
			for (int i = 0; i < parts.Length; i++)
			{
				if (!this.PartDefinitions[i].Property.PropertyType.IsAssignableFrom(values[i].GetType()))
					return false;
			}

			return true;
		}

		private bool ValidateConstraints(IDictionary<IEntityProperty, object> propertyValues, bool throwEx = true)
		{
			bool valid = true;
			foreach (var constraint in this.Constraints)
			{
				object val;
				var validateFunction = constraint.Value;
				if (!propertyValues.TryGetValue(constraint.Key, out val) || !validateFunction(val))
				{
					valid = false;
					break;
				}
			}

			if (!valid && throwEx)
				throw new IdentityConstraintException("Constraint is not valid.");

			return valid;
		}

		private bool ValidateConstraints(object obj, bool throwEx = true)
		{
			bool valid = true;
			foreach (var constraint in this.Constraints)
			{
				object val = constraint.Key.GetValue(obj);
				var validateFunction = constraint.Value;
				if (!validateFunction(val))
				{
					valid = false;
					break;
				}
			}

			if (!valid && throwEx)
				throw new IdentityConstraintException("Constraint is not valid.");

			return valid;
		}
	}

	[Serializable]
	public class IdentityConstraintException : Exception
	{
		public IdentityConstraintException() { }
		public IdentityConstraintException(string message) : base(message) { }
		public IdentityConstraintException(string message, Exception inner) : base(message, inner) { }
		protected IdentityConstraintException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	public struct IdentityPart
	{
		public readonly IdentityPartDefinition PartDefinition;
		public readonly object Value;
		private int _hash;

		public IdentityPart(IdentityPartDefinition partDefinition, object value)
		{
			PartDefinition = partDefinition;
			Value = value;

			var hashinput = new object[2];
			hashinput[0] = partDefinition;
			hashinput[1] = value;
			_hash = HashingHelper.Hash(hashinput);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IdentityPart))
				return false;

			IdentityPart otherPart = (IdentityPart)obj;
			return
				Object.Equals(otherPart.PartDefinition, this.PartDefinition) &&
				Object.Equals(otherPart.Value, this.Value);
		}

		public override int GetHashCode()
		{
			return _hash;
		}

		public override string ToString()
		{
			return String.Format("{0}={1}", PartDefinition, Value);
		}
	}

	public struct Identity
	{
		public readonly IdentityDefinition IdentityDefinition;
		public readonly IdentityPart[] Parts;
		private int _hash;

		public Identity(IdentityDefinition definition, IdentityPart[] parts)
		{
			if (definition == null)
				throw new ArgumentNullException("definition");

			if (parts == null || parts.Length < 1)
				throw new ArgumentException("Parts cannot be null or zero length", "parts");

			this.IdentityDefinition = definition;
			this.Parts = new IdentityPart[parts.Length];
			parts.CopyTo(Parts, 0);

			var hashinput = new object[parts.Length + 1];
			hashinput[0] = definition;
			parts.CopyTo(hashinput, 1);
			_hash = HashingHelper.Hash(hashinput);

		}

		public override bool Equals(object obj)
		{
			if (!(obj is Identity))
				return false;

			Identity keyToCompare = (Identity)obj;

			if (keyToCompare.IdentityDefinition != this.IdentityDefinition)
				return false;

			// Segment length must match
			if (keyToCompare.Parts.Length != Parts.Length)
				return false;

			// Compare the order of the parts and their values
			for (int i = 0; i < Parts.Length; i++)
			{
				if (!Object.Equals(Parts[i].PartDefinition, keyToCompare.Parts[i].PartDefinition))
					return false;

				if (!Object.Equals(Parts[i].Value, keyToCompare.Parts[i].Value))
					return false;
			}

			return true;
		}

		public override int GetHashCode()
		{
			return _hash;
		}

		public bool IsEmpty
		{
			get { return this.IdentityDefinition == null; }
		}

		public override string ToString()
		{
			string output = String.Empty;
			for (int i = 0; i < Parts.Length; i++)
			{
				output += Parts[i].ToString();
				if (i < Parts.Length - 1)
					output += ";";
			}
			return output;
		}
	}

	// ..........................................................................................
	// FROM http://stackoverflow.com/questions/1079192/is-it-possible-to-combine-hash-codes-for-private-members-to-generate-a-new-hash

	internal static class HashingHelper
	{
		private static unsafe void Hash(byte* d, int len, ref uint h)
		{
			for (int i = 0; i < len; i++)
			{
				h += d[i];
				h += (h << 10);
				h ^= (h >> 6);
			}
		}

		public unsafe static void Hash(ref uint h, string s)
		{
			fixed (char* c = s)
			{
				byte* b = (byte*)(void*)c;
				Hash(b, s.Length * 2, ref h);
			}
		}

		public unsafe static void Hash(ref uint h, int data)
		{
			byte* d = (byte*)(void*)&data;
			Hash(d, sizeof(int), ref h);
		}

		public unsafe static int Avalanche(uint h)
		{
			h += (h << 3);
			h ^= (h >> 11);
			h += (h << 15);
			return *((int*)(void*)&h);
		}

		public static int Hash(Array arrayValues)
		{
			uint h = 0;

			// Combine hashcodes with the remaining segments
			for (int i = 0; i < arrayValues.Length; i++)
			{
				object val = arrayValues.GetValue(i);
				Hash(ref h, val == null ? 0 : val.GetHashCode());
			}

			return Avalanche(h);
		}
	}
}
