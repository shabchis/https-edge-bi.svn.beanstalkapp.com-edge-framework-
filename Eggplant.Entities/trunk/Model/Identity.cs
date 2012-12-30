using System;
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

		public IdentityDefinition(params IEntityProperty[] parts)
		{
			if (parts == null || parts.Length < 1)
				throw new ArgumentException("One or more properties are required.", "parts");

			PartDefinitions = new IdentityPartDefinition[parts.Length];
			for (int i = 0; i < parts.Length; i++)
				PartDefinitions[i] = new IdentityPartDefinition(parts[i]);
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
			return HashingHelper.Hash(this.PartDefinitions);
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

			return new Identity(parts);
		}

		
		public Identity NewIdentity(params object[] values)
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

			return new Identity(parts);
		}
	}

	public class IdentityPart
	{
		public IdentityPartDefinition PartDefinition;
		public object Value;

		internal IdentityPart(IdentityPartDefinition partDefinition, object value)
		{
			PartDefinition = partDefinition;
			Value = value;
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
			return HashingHelper.Hash(new object[] { PartDefinition, Value});
		}

		public override string ToString()
		{
			return String.Format("{0}={1}", PartDefinition, Value);
		}
	}

	public class Identity
	{
		public readonly IdentityPart[] Parts;

		internal Identity(IdentityPart[] parts)
		{
			if (parts == null || parts.Length < 1)
				throw new ArgumentException("Parts cannot be null or zero length", "parts");

			Parts = new IdentityPart[parts.Length];
			parts.CopyTo(Parts, 0);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Identity))
				return false;

			Identity keyToCompare = (Identity)obj;

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
			return HashingHelper.Hash(this.Parts);
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
