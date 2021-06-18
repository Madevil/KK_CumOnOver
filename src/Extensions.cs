using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;

using HarmonyLib;

namespace CumOnOver
{
	public static partial class Extensions
	{
		public static object RefTryGetValue(this object _self, object _key)
		{
			if (_self == null) return null;

			MethodInfo _tryMethod = AccessTools.Method(_self.GetType(), "TryGetValue");
			object[] _parameters = new object[] { _key, null };
			_tryMethod.Invoke(_self, _parameters);
			return _parameters[1];
		}

		public static T RefTryGetValue<T>(this object _self, object _key)
		{
			if (_self == null)
				return default(T);
			MethodInfo _tryMethod = AccessTools.Method(_self.GetType(), "TryGetValue");
			object[] _parameters = new object[] { _key, null };
			_tryMethod.Invoke(_self, _parameters);
			if (_parameters[1] == null)
				return default(T);
			return (T) _parameters[1];
		}

		public static object RefElementAt(this object _self, int _key)
		{
			if (_self == null)
				return null;
			if (_key > (Traverse.Create(_self).Property("Count").GetValue<int>() - 1))
				return null;

			return Traverse.Create(_self).Method("get_Item", new object[] { _key }).GetValue();
		}

		public static T RefElementAt<T>(this object _self, int _key)
		{
			if (_self == null)
				return default(T);
			if (_key > (Traverse.Create(_self).Property("Count").GetValue<int>() - 1))
				return default(T);

			return Traverse.Create(_self).Method("get_Item", new object[] { _key }).GetValue<T>();
		}
	}
}
