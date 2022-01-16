using System;
using System.Dynamic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace NodoAme.Models
{
	class COMObject : DynamicObject, IDisposable
	{
		private object _instance;

		public static COMObject CreateObject(string progID)
			=> new COMObject(Activator.CreateInstance(Type.GetTypeFromProgID(progID, true)));

		public COMObject(object instance)
		{
			if (instance is null)
			{
				throw new ArgumentNullException(nameof(instance));
			}
			if (!instance.GetType().IsCOMObject)
			{
				throw new ArgumentException("Object must be a COM object", nameof(instance));
			}
			_instance = instance;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			result = Unwrap().GetType().InvokeMember(
				binder.Name,
				BindingFlags.GetProperty,
				Type.DefaultBinder,
				Unwrap(),
				new object[] { }
			);
			return true;
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			Unwrap().GetType().InvokeMember(
				binder.Name,
				BindingFlags.SetProperty,
				Type.DefaultBinder,
				Unwrap(),
				new object[] { WrapIfRequired(value) }
			);
			return true;
		}

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
		{
			for (int i = 0; i < args.Length; i++)
			{
				if(args[i] is COMObject co)
				{
					args[i] = co.Unwrap();
				}
			}
			result = Unwrap().GetType().InvokeMember(
				binder.Name,
				BindingFlags.InvokeMethod,
				Type.DefaultBinder,
				Unwrap(),
				args
			);
			result = WrapIfRequired(result);
			return true;
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			result = WrapIfRequired(
				Unwrap()!.GetType()
					.InvokeMember(
						"Item",
						BindingFlags.GetProperty,
						Type.DefaultBinder,
						Unwrap(),
						indexes
					));
			return true;
		}

		private object Unwrap()
			=> _instance ?? throw new ObjectDisposedException(nameof(_instance));

		private static object? WrapIfRequired(object obj)
			=> obj?.GetType()?.IsCOMObject == true ? new COMObject(obj) : obj;

		public void Dispose()
		{
			// The RCW is a .NET object and cannot be released from the finalizer,
			// because it might not exist anymore.
			var toBeDisposed = Interlocked.Exchange(ref _instance, null);
			if (toBeDisposed != null)
			{
				Marshal.ReleaseComObject(toBeDisposed);
				GC.SuppressFinalize(this);
			}
		}
	}

}
