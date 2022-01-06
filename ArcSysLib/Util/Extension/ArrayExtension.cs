namespace ArcSysLib.Utils.Extensions
{
    public static class ArrayExtension
    {
        public static void Populate<T>(this T[] arr, T value)
        {
            for (var i = 0; i < arr.Length; i++) arr[i] = value;
        }
    }
}