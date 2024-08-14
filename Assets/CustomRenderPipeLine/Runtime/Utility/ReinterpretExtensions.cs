using System.Runtime.InteropServices;

public static class ReinterpretExtensions
{
    [StructLayout(LayoutKind.Explicit)]
    struct IntFloat
    {
        //将两个字段的存储偏移都设置为0，以此二者重叠（都是四个字节）
        [FieldOffset(0)]
        public int intValue;

        [FieldOffset(0)]
        public float floatValue;
    }

    //扩展方法 将int重新解释为float 否则在重定向layermask时可能出现错误的对其
    public static float ReinterpretAsFloat(this int value)
    {
        IntFloat converter = default;
        converter.intValue = value;
        return converter.floatValue;
    }
}