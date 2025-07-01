using System.Collections.Generic;

namespace WpfApp
{
    public class ProjectInfo
    {
        public string Path { get; set; }
        public string DisplaySize { get; set; }
        public double Percentage { get; set; }
        public List<UstInfo> UstList { get; set; }
    }
}