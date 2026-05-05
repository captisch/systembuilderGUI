namespace systembuilderGUI.Models
{
    public struct Wire(string name, int size, bool isTop, bool? hasDriver)
    {
        public string Name { get; set; } = name;
        public int Size { get; set; } = size;

        public bool IsTop { get; set; } = isTop;
        //this indicates whether the wire is a top module signal
        public bool?[] HasDriver { get; set; } = new bool?[size];
    }
}