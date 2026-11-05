using Sandbox.Engine.Multiplayer;
using System;
using System.Collections.Generic;
using System.Reflection;
using VRageMath;
using VRage.Network;

namespace SensEssentials.Utils;

public static class Events 
{
    private static Dictionary<MethodInfo, Delegate> _delegateCache = [];
    
    private static Func<T, TA>? GetDelegate<T, TA>(MethodInfo method) where TA : class 
    {
        if (!_delegateCache.TryGetValue(method, out Delegate? del)) 
        {
            MethodInfo? invokeMethod = typeof(TA).GetMethod("Invoke");
            if (invokeMethod == null) return null;
            
            Delegate targetDel = Delegate.CreateDelegate(typeof(TA), null, method);
            
            del = new Func<T, TA>(instance => (TA)(object)targetDel);
            
            _delegateCache[method] = del;
        }

        return (Func<T, TA>)del;
    }

    public static void RaiseEvent<T1, T2, T3, T4, T5>(T1 instance, MethodInfo method, T2 arg1, T3 arg2, T4 arg3, T5 arg4, EndpointId target = default) where T1 : IMyEventOwner 
    {
        var del = GetDelegate<T1, Action<T2, T3, T4, T5>>(method);
        if (del is null) return;

        MyMultiplayer.RaiseEvent(instance, del, arg1, arg2, arg3, arg4, target);
    }
}
