using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Model.Queries
{
	public interface IQueryResultReader
	{
	}

	public abstract class QueryResultReader<ResultT> : IDisposable where ResultT : class
	{
		#region Members
		/*=========================*/

		private bool _readerOpen = false;
		private ResultT _currentResult = null;

		/*=========================*/
		#endregion

		#region Access Methods
		/*=========================*/

		public ResultT Current
		{
			get
			{
				return _currentResult;
			}
		}

		/*=========================*/
		#endregion

		#region Public Methods
		/*=========================*/

		public bool Read()
		{
			if (!_readerOpen)
			{
				OpenReader();
				_readerOpen = true;
			}

			_currentResult = GetResult();

			return _currentResult != null;
		}

		public virtual void Dispose()
		{
		}

		/*=========================*/
		#endregion

		#region Internal Methods
		/*=========================*/

		protected abstract ResultT GetResult();

		protected virtual void OpenReader()
		{
		}

		/*=========================*/
		#endregion

	}
}
