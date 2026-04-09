namespace MatchZy.Util
{
    public struct HexColor(int value)
    {
        public int Value { get; } = value;
        public string Hex { get; } = $"#{value:X6}";

        public static implicit operator HexColor(int value) => new(value);

        public static implicit operator int(HexColor color) => color.Value;
    }

    public static class HexColors
    {
        public static HexColor Red => new(0xFF0000);
        public static HexColor Green => new(0x00FF00);
        public static HexColor Blue => new(0x0000FF);
        public static HexColor White => new(0xFFFFFF);
    }
}
