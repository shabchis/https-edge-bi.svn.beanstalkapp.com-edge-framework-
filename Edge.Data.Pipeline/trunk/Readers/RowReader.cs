using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;

namespace Edge.Data.Pipeline
{

	public abstract class RowReader<RowT> : IRowReader<RowT> where RowT: class
	{
		#region Fields
		/*=========================*/

		private bool _readerOpen = false;
		private RowT _currentRow = null;

		/*=========================*/
		#endregion

		#region Core functionality
		/*=========================*/

		public RowT CurrentRow
		{
			get
			{
				return _currentRow;
			}
		}

		public bool Read()
		{
			if (!_readerOpen)
			{
				Open();
				_readerOpen = true;
			}

			// Call this abstract function to 
			// initalize the current row with the xml row.
			_currentRow = NextRow();

			if (_currentRow == null)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		/*=========================*/
		#endregion

		#region Abtract
		/*=========================*/

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected abstract RowT NextRow();

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

		#region IRowReader Members
		/*=========================*/

		object IRowReader.CurrentRow
		{
			get { return this.CurrentRow; }
		}

		/*=========================*/

		#endregion	
	}
}
