namespace ViewModels
{
    public class NameModel
    {
        public NameModel(string first, string last)
        {
            this.First = first;
            this.Last = last;
        }

        public string First { get; }

        public string Last { get; }

        public override string ToString()
        {
            return $"Name: [{this.First}] [{this.Last}]";
        }
    }
}
