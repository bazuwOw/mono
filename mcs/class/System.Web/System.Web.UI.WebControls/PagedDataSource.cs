// System.Web.UI.WebControls.PagedDataSource.cs
//
// Author: Duncan Mak (duncan@novell.com)
//	   Jackson Harper  (jackson@ximian.com)	
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Collections;
using System.ComponentModel;
using System.Security.Permissions;

namespace System.Web.UI.WebControls {

	// CAS - no inheritance demand required because the class is sealed
	[AspNetHostingPermission (SecurityAction.LinkDemand, Level = AspNetHostingPermissionLevel.Minimal)]
	public sealed class PagedDataSource : ICollection, IEnumerable, ITypedList
	{
		int page_size, current_page_index, virtual_count;
		bool allow_paging, allow_custom_paging, allow_server_paging;
		IEnumerable source;
		
		public PagedDataSource ()
		{
			page_size = 10;
		}

		public bool AllowCustomPaging {
			get { return allow_custom_paging; }
			set { 
				allow_custom_paging = value; 
#if NET_2_0
				// AllowCustomPaging and AllowServerPaging are mutually exclusive
				if (allow_custom_paging)
					allow_server_paging = false;
#endif
			}
		}

		public bool AllowPaging {
			get { return allow_paging; }
			set { allow_paging = value; }
		}

		public int Count {
			get {
				if (source == null)
					return 0;
				
				if (IsPagingEnabled) {
					if (IsCustomPagingEnabled || !IsLastPage)
						return page_size;
					return DataSourceCount - FirstIndexInPage;
				}

				return DataSourceCount;
			}
		}						

		public int CurrentPageIndex {
			get { return current_page_index; }
			set { current_page_index = value; }
		}

		public IEnumerable DataSource {
			get { return source; }
			set { source = value; }
		}

		public int DataSourceCount {
			get {
				if (source == null)
					return 0;
				
				if (IsCustomPagingEnabled 
#if NET_2_0
				    || IsServerPagingEnabled
#endif
				)
					return virtual_count;

				if (source is ICollection)
					return ((ICollection) source).Count;

				throw new HttpException ("The data source must implement ICollection");
			}
		}

		public int FirstIndexInPage {
			get {
				if (!IsPagingEnabled || IsCustomPagingEnabled || 
#if NET_2_0
				    IsServerPagingEnabled || 
#endif
				    source == null)
					return 0;
				
				return current_page_index * page_size;
			}
		}

		public bool IsCustomPagingEnabled {
			get { return IsPagingEnabled && allow_custom_paging; }
		}

#if NET_2_0
		public bool IsServerPagingEnabled {
			get { return IsPagingEnabled && allow_server_paging; }
		}
#endif

		public bool IsFirstPage {
			get { 
				if (!allow_paging)
					return true;
				
				return current_page_index == 0; 
			}
		}

		public bool IsLastPage {
			get {
				if (!allow_paging || page_size == 0)
					return true;

				return  (current_page_index == (PageCount - 1));
			}
		}

		public bool IsPagingEnabled {
			get { return (allow_paging && page_size != 0); }
		}

		public bool IsReadOnly {
			get { return false; } // as documented
		}

		public bool IsSynchronized {
			get { return false; } // as documented
		}

		public int PageCount {
			get {
				if (source == null)
					return 0;
				
				if (!IsPagingEnabled || DataSourceCount == 0 || page_size == 0)
					return 1;
				
				return (DataSourceCount + page_size - 1) / page_size;
			}
		}
		
		public int PageSize {
			get { return page_size; }
			set { page_size = value; }
		}

		public object SyncRoot {
			get { return this; }
		}

		public int VirtualCount {
			get { return virtual_count; }
			set { virtual_count = value; }
		}
#if NET_2_0
		public bool AllowServerPaging {
			get {
				return allow_server_paging;
			}
			set {
				allow_server_paging = value;
				// AllowCustomPaging and AllowServerPaging are mutually exclusive
				if (allow_server_paging)
					allow_custom_paging = false;
			}
		}
#endif

		public void CopyTo (Array array, int index)
		{
			foreach (object o in source)
				array.SetValue (o, index++);
		}

		public IEnumerator GetEnumerator ()
		{
			// IList goes first, as it implements ICollection
			IList list = source as IList;
			int first = 0;
			int count;
			int limit;
			if (list != null) {
				first = FirstIndexInPage;
				count = ((ICollection) source).Count;
				limit = ((first + page_size) > count) ? (count - first) : page_size;
				return GetListEnum (list, first, first + limit);
			}

			ICollection col = source as ICollection;
			if (col != null) {
				first = FirstIndexInPage;
				count = col.Count;
				limit = ((first + page_size) > count) ? (count - first) : page_size;
				return GetEnumeratorEnum (col.GetEnumerator (), first, first + page_size);
			}

			return source.GetEnumerator ();
		}

		public PropertyDescriptorCollection GetItemProperties (PropertyDescriptor [] list_accessors)
		{
			ITypedList typed = source as ITypedList;
			if (typed == null)
				return null;
			return typed.GetItemProperties (list_accessors);
		}

		public string GetListName (PropertyDescriptor [] list_accessors)
		{
			return String.Empty; // as documented
		}

#if TARGET_JVM
		internal class ListEnum : IEnumerator
		{
			int start;
			int end;
			int ind;
			IList list;

			internal ListEnum(IList list, int start, int end)
			{
				this.list = list;
				this.start = start;
				this.end = end;
				this.ind = start - 1;
			}

			public bool MoveNext()
			{
				ind++;
				return (ind < end);
			}

			public void Reset() { ind = start - 1; }
			public object Current { get { return list[ind]; }}
		}

		private IEnumerator GetListEnum (IList list, int start, int end)
		{
			if (!allow_paging)
				end = list.Count;
			return new ListEnum(list, start, end);
		}

		internal class EnumeratorEnum : IEnumerator
		{
			int start;
			int end;
			int ind;
			IEnumerator en;
			PagedDataSource parent;

			internal EnumeratorEnum(PagedDataSource parent, IEnumerator en, int start, int end)
			{
				this.parent = parent;
				this.en = en;
				this.start = start;
				this.end = end;
				this.ind = start - 1;
				for (int i = 0; i < start; i++)
					en.MoveNext ();
			}

			public bool MoveNext()
			{
				ind++;
				return (!parent.allow_paging || ind < end) && en.MoveNext ();
			}

			public void Reset()
			{
				throw new NotSupportedException();
			}

			public object Current { get { return en.Current; }}
		}

		private IEnumerator GetEnumeratorEnum (IEnumerator e, int start, int end)
		{
			return new EnumeratorEnum(this, e, start, end);
		}
#else
		private IEnumerator GetListEnum (IList list, int start, int end)
		{
			if (!AllowPaging)
				end = list.Count;
			else if (start >= list.Count)
				yield break;
			
			for (int i = start; i < end; i++)
				yield return list [i];
		}

		private IEnumerator GetEnumeratorEnum (IEnumerator e, int start, int end)
		{
			for (int i = 0; i < start; i++)
				e.MoveNext ();
			for (int i = start; (!allow_paging || i < end) && e.MoveNext (); i++)
				yield return e.Current;
		}
#endif
	}
}

