using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.ComponentModel;
using System.Collections;

namespace Eggplant.Persistence
{
	public interface IEntityList: IBindingList
	{
	}

	/// <summary>
	/// 
	/// </summary>
	public class EntityList<EntityT>: BindingList<EntityT>, IEntityList where EntityT: Entity, new()
	{
		#region Fields
		/*=========================*/

		private Dictionary<IndexDefinition, Dictionary<IndexEntry, EntityT>> _indexes;
		private List<EntityT> _originals = new List<EntityT>();
		private IEntityProperty _ordProperty = null;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="keyProperties"></param>
		public EntityList()
		{
			_indexes = new Dictionary<IndexDefinition, Dictionary<IndexEntry, EntityT>>();
		}

		/*=========================*/
		#endregion

		#region Indexing-related Methods
		/*=========================*/

		public void AddListIndex(IndexDefinition definition)
		{
			if (_indexes.ContainsKey(definition))
				throw new ArgumentException("The key definition is already used by an index in this list.");

			Dictionary<IndexEntry, EntityT> newIndex = new Dictionary<IndexEntry, EntityT>(this.Count);

			try
			{
				foreach (EntityT entity in this.Items)
				{
					newIndex.Add(definition.CreateIndexEntry(entity), entity);
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Failed to create index.", ex);
			}

			_indexes.Add(definition, newIndex);
		}

		public void AddListIndex(params IEntityProperty[] segmentProperties)
		{
			AddListIndex(IndexDefinition.WithSegments(segmentProperties));
		}

		public void RemoveListIndex(IndexDefinition definition)
		{
			_indexes.Remove(definition);
		}

		public void RemoveListIndex(params IEntityProperty[] segmentProperties)
		{
			_indexes.Remove(IndexDefinition.WithSegments(segmentProperties));
		}

		private Dictionary<IndexDefinition, IndexEntry> CreateIndexEntries(EntityT item)
		{
			Dictionary<IndexDefinition, IndexEntry> indexEntries = new Dictionary<IndexDefinition, IndexEntry>();
			foreach (KeyValuePair<IndexDefinition, Dictionary<IndexEntry, EntityT>> kvPair in _indexes)
			{
				IndexDefinition definition = kvPair.Key;
				Dictionary<IndexEntry, EntityT> index = kvPair.Value;

				// Check unique value
				IndexEntry key = definition.CreateIndexEntry(item);

				// EXCEPTION:
				if (key != null && index.ContainsKey(key))
					throw new EntityIndexException(String.Format("An item with the identity [{0}] already exists in the list.", key));

				// Remember for later
				indexEntries.Add(definition, key);
			}
			return indexEntries;
		}

		private void ApplyIndexEntries(EntityT item, Dictionary<IndexDefinition, IndexEntry> indexEntries)
		{
			// Apply the index entries we found earlier
			foreach (KeyValuePair<IndexDefinition, IndexEntry> indexEntry in indexEntries)
				_indexes[indexEntry.Key].Add(indexEntry.Value, item);

		}

		private void RemoveFromIndexes(EntityT item)
		{
			// Remove from indexes
			foreach (KeyValuePair<IndexDefinition, Dictionary<IndexEntry, EntityT>> kvPair in _indexes)
			{
				IndexDefinition definition = kvPair.Key;
				Dictionary<IndexEntry, EntityT> index = kvPair.Value;

				IndexEntry key = definition.CreateIndexEntry(item);
				index.Remove(key);
			}

		}

		private void ClearIndexes()
		{
			foreach (Dictionary<IndexEntry, EntityT> index in _indexes.Values)
				index.Clear();
		}

		private void BuildIndexes()
		{
			// Remove from indexes
			foreach (KeyValuePair<IndexDefinition, Dictionary<IndexEntry, EntityT>> kvPair in _indexes)
			{
				IndexDefinition definition = kvPair.Key;
				Dictionary<IndexEntry, EntityT> index = kvPair.Value;

				index.Clear();
				foreach (EntityT item in this.Items)
					index.Add(definition.CreateIndexEntry(item), item);
			}
		}

		/*=========================*/
		#endregion

		#region BindingList<T> overrides
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		protected override void InsertItem(int index, EntityT item)
		{
			// Determine whether this is a valid item; duplicate it if it belongs to a different list
			if (item.ParentList != this)
				item = ImportItem(item);

			else if (item.DataState != EntityDataState.Deleted && item.DataState != EntityDataState.Detached)
				throw new InvalidOperationException("Cannot add items that are already in the list.");

			// Save original list
			StoreOriginals();

			// Create index entries, throw exceptions if non-unique
			Dictionary<IndexDefinition, IndexEntry> indexEntries = CreateIndexEntries(item);
			
			// Mark ordinals if property is available
			if (_ordProperty != null)
			{
				_ordProperty.SetValue(item, index);

				for (int i = index + 1; i < this.Count; i++)
					_ordProperty.SetValue(this[i], i);
			}

			// Apply the indexes now
			ApplyIndexEntries(item, indexEntries);
			
			// Remove the item from the deleted list since we're re-adding it
			if (item.DataState == EntityDataState.Deleted)
				item.DataState = item.HasValueChanges ? EntityDataState.Modified : EntityDataState.Unchanged;

			// Insert the item
			base.InsertItem(index, item);
		}

		private EntityT ImportItem(EntityT item)
		{
			throw new NotImplementedException();
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		protected override void RemoveItem(int index)
		{
			// Save original list
			StoreOriginals();

			EntityT item = this.Items[index];

			// Change state accordingly
			SetItemRemoved(item);

			// Remove the item from all indexes
			RemoveFromIndexes(item);

			base.RemoveItem(index);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="index"></param>
		/// <param name="item"></param>
		protected override void SetItem(int index, EntityT item)
		{
			throw new NotSupportedException();
			//base.SetItem(index, item);
		}

		/// <summary>
		/// 
		/// </summary>
		protected override void ClearItems()
		{
			// Save original list
			StoreOriginals();

			// Clear indexes
			ClearIndexes();

			foreach(EntityT t in this.Items)
				SetItemRemoved(t);

			base.ClearItems();
		}

		protected override object AddNewCore()
		{
			EntityT item = new EntityT();
			item.ParentList = this;
			return item;
		}

		private void StoreOriginals()
		{
			if (_originals == null)
				_originals = new List<EntityT>(this.Items);
		}

		private void SetItemRemoved(EntityT item)
		{
			if (item.DataState == EntityDataState.Added)
				item.DataState = EntityDataState.Detached;
			else
				item.DataState = EntityDataState.Deleted;
		}

		/*=========================*/
		#endregion

		/// <summary>
		/// 
		/// </summary>
		public void AcceptChanges()
		{
			foreach (EntityT entity in this.Items)
			{
				entity.AcceptChanges();
			}

			_originals.Clear();
			_originals = null;
		}

		/// <summary>
		/// 
		/// </summary>
		public void RejectChanges()
		{
			this.Items.Clear();

			foreach (EntityT entity in _originals)
			{
				entity.RejectChanges();
				this.Items.Add(entity);
			}

			_originals.Clear();
			_originals = null;
		}

		public void Save()
		{
			//this.Adapter.Persist(PersistMode.Transaction);
		}
		// Eggplant-TODO:
		//public PersistenceAdapter Adapter
		//{
		//    get
		//    {
		//        if (PersistenceProvider.Connection == null)
		//            return null;

		//        PersistenceProvider current = PersistenceProvider.Connection.Provider;
		//        PersistenceAdapter adapter;
		//        if (!_adapters.TryGetValue(current, out adapter))
		//        {
		//            adapter = current.CreateAdapter(this.GetType());
		//            _adapters.Add(current, adapter);
		//        }

		//        return adapter;
		//    }
		//    set
		//    {
		//        // TODO: support explicit adpater setting
		//    }
		//}

	}

	public class IndexDefinition
	{
		public readonly IEntityProperty[] SegmentProperties;

		private IndexDefinition(params IEntityProperty[] segmentProperties)
		{
			if (segmentProperties == null || segmentProperties.Length < 1)
				throw new ArgumentException("One or more properties are required as segments", "segmentProperties");

			SegmentProperties = segmentProperties;
		}

		public static IndexDefinition WithSegments(params IEntityProperty[] segmentProperties)
		{
			return new IndexDefinition(segmentProperties);
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IndexDefinition))
				return false;

			IndexDefinition defToCompare = (IndexDefinition)obj;

			// Segment length must match
			if (defToCompare.SegmentProperties.Length != this.SegmentProperties.Length)
				return false;

			// Compare the order of the segments and their values
			for (int i = 0; i < this.SegmentProperties.Length; i++)
			{
				if (this.SegmentProperties[i] != defToCompare.SegmentProperties[i])
					return false;
			}

			return true;
		}

		public override int GetHashCode()
		{
			if (SegmentProperties.Length < 1)
				return 0;

			int hashCode = SegmentProperties[0].GetHashCode();

			// Combine hashcodes with the remaining segments
			for (int i = 1; i < SegmentProperties.Length; i++)
			{
				hashCode ^= SegmentProperties[i].GetHashCode();
			}

			return hashCode;
		}

		public override string ToString()
		{
			string output = String.Empty;
			for (int i = 0; i < SegmentProperties.Length; i++)
			{
				output += SegmentProperties[i].InlineName;
				if (i < SegmentProperties.Length - 1)
					output += "-";
			}
			return output;
		}

		/// <summary>
		/// Generate a key from a specified entity;
		/// </summary>
		/// <param name="entity"></param>
		/// <returns></returns>
		public IndexEntry CreateIndexEntry(Entity entity)
		{
			List<IndexEntrySegment> segments = new List<IndexEntrySegment>();
			foreach (IEntityProperty property in this.SegmentProperties)
			{
				segments.Add(new IndexEntrySegment(property, property.GetValue(entity)));
			}

			return new IndexEntry(segments.ToArray());
		}
	}

	public class IndexEntrySegment
	{
	    public IEntityProperty Property;
		public object Value;

		internal IndexEntrySegment(IEntityProperty property, object value)
		{
			Property = property;
			Value = value;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IndexEntrySegment))
				return false;

			IndexEntrySegment otherSegment = (IndexEntrySegment) obj;
			return otherSegment.Property == Property && otherSegment.Value == Value;
		}

		public override int GetHashCode()
		{
			return Property.GetHashCode() ^ Value.GetHashCode();
		}

		public override string ToString()
		{
			return String.Format("{0}: {1}", Property.InlineName, Value);
		}
	}

	public class IndexEntry
	{
		public readonly IndexEntrySegment[] Segments;

		internal IndexEntry(IndexEntrySegment[] segments)
		{
			if (segments == null || segments.Length < 1)
				throw new ArgumentException("Segments cannot be null or zero length", "segments");

			Segments = segments;
		}

		public override bool Equals(object obj)
		{
			if (!(obj is IndexEntry))
				return false;

			IndexEntry keyToCompare = (IndexEntry) obj;

			// Segment length must match
			if (keyToCompare.Segments.Length != Segments.Length)
				return false;

			// Compare the order of the segments and their values
			for(int i = 0; i < Segments.Length; i++)
			{
				if (Segments[i].Property != keyToCompare.Segments[i].Property)
					return false;

				if (!Segments[i].Value.Equals(keyToCompare.Segments[i].Value))
					return false;
			}

			return true;
		}

		public override int GetHashCode()
		{
			if (Segments.Length < 1)
				return 0;

			int hashCode = Segments[0].GetHashCode();

			// Combine hashcodes with the remaining segments
			for(int i = 1; i < Segments.Length; i++)
			{
				hashCode ^= Segments[i].GetHashCode();
			}

			return hashCode;
		}

		public override string ToString()
		{
			string output = String.Empty;
			for(int i = 0; i < Segments.Length; i++)
			{
				output += Segments[i].ToString();
				if (i < Segments.Length - 1)
					output += "-";
			}
			return output;
		}
	}
}
