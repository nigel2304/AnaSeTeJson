public class IssuesJiraModel
{
    public class IssuesJira
    {
        public List<Issues> issues { get; set; } = new List<Issues>();
    }

    public class Issues
    {
        public string? id { get; set; }
        public string? key { get; set; }
        public Fields fields { get; set; } = new Fields();
        public Changelog changelog { get; set; } = new Changelog();
    }

    public class Fields
    {
        public string? summary { get; set; }
    }

    public class Changelog
    {
        public List<Histories> histories { get; set; } = new List<Histories>();

    }

    public class Histories
    {
        public string? id { get; set; }
        public string? created { get; set; }
        public Author? author { get; set; } = new Author();
        public List<Items> items { get; set; } = new List<Items>();
    }

    public class Author
    {
        public string? name { get; set; }
        public string? key { get; set; }
        public string? emailAddress { get; set; }
        public string? displayName { get; set; }
    }

    public class Items
    {
        public string? field { get; set; }
        public string? fieldtype { get; set; }
        public string? from { get; set; }
        public string? fromString { get; set; }
        public string? to { get; set; }
        public string? active { get; set; }
        public string? toString { get; set; }
    }    

    public class IssuesResult
    {
        public string? Id { get; set; }
        public string? Key { get; set; }
        public string? Summary { get; set; }
        public string? Sprint { get; set; }
        public string? HistorySprint { get; set; }
        public string? Replanning { get; set; }
        public List<IssuesResultHistories> IssuesResultHistories { get; set; } = new List<IssuesResultHistories>();
    }

    public class IssuesResultHistories
    {
        public string? UserKey { get; set; }
        public string? UserName { get; set; }
        public string? DateChangeStatus { get; set; }
        public int CycleTime { get; set; }
        public int CycleTimeWorkDays { get; set; }
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
    }
}