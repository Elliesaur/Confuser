using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;


public static class PolyInjection
{
    public delegate double CallDM(double t);

    public static CallDM Invoke;

    static DynamicMethod dm;

    static PolyInjection()
    {
        Initialize();
    }

    public static void Initialize()
    {
        dm = new DynamicMethod(PolyMutation.MethodName, typeof(Double), new Type[] { typeof(Double) });

        ILGenerator il = dm.GetILGenerator();


        // The rest of the emits go here
        // We know if we hit the (Ldsfld - OpCodes.Ret) then we will be too far, so we go back two (since load ilgen) and start inserting emit calls.

        il.Emit(OpCodes.Conv_R8);
        // Return
        il.Emit(OpCodes.Ret);

        // Assign to delegate
        Invoke = (CallDM)dm.CreateDelegate(typeof(CallDM));


    }
}

public static class PolyMutation
{
    public static double Number;
    public static OpCode Operation;
    public static string MethodName;
    public static Type ReturnType;
    public static Type[] Parameters;

}

