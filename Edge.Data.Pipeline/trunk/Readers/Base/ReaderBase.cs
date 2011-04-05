using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;

namespace Edge.Data.Pipeline.Readers
{

	public abstract class ReaderBase<T> : IReader<T>
	{
		#region Fields
		/*=========================*/

		private bool _readerOpen = false;
		private bool _hasCurrent;
		private T _current;

		/*=========================*/
		#endregion

		#region Core functionality
		/*=========================*/

		public bool HasCurrent
		{
			get { return _hasCurrent; }
		}

		public T Current
		{
			get
			{
				return _current;
			}
		}

		public bool Read()
		{
			if (!_readerOpen)
			{
				Open();
				_readerOpen = true;
			}

			_hasCurrent = Next(ref _current);
			if (!_hasCurrent)
				_current = default(T);

			return _hasCurrent;
		}

		/*=========================*/
		#endregion

		#region Abtract
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected abstract bool Next(ref T next);

		/// <summary>
		/// 
		/// </summary>
		protected abstract void Open();

		/// <summary>
		/// 
		/// </summary>
		public abstract void Dispose();

		/*=========================*/
		#endregion

		#region IReader Members
		/*=========================*/

		object IReader.Current
		{
			get { return this.Current; }
		}

		/*=========================*/

		#endregion	
	}
}
