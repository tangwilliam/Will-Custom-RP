
using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    // union struct。 可以用来准确地将int 转换为float。以在Shader端正确将float转回为uint
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }

    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue;
    }
}