namespace WpfApp
{
    public class UstInfo
    {
        public string Path { get; set; }
        public string DisplaySize { get; set; }
        public double Percentage { get; set; }

        public override string ToString() => System.IO.Path.GetFileName(Path);
    }
}