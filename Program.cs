using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Serialization;

namespace DeserializeFromFile
{
    public class AnaSete
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

    
    public class Program
    {
        public static void Main()
        {
            try
            {
                var baseDirecotry = AppDomain.CurrentDomain.BaseDirectory;
                var fileNameSource = baseDirecotry + "SourceEstoriasJiraAPI.json";
                var fileNameJsonClose = baseDirecotry + "EstoriasTransactionsFormatterJSON.json";
                var fileNameXmlClose = baseDirecotry + "EstoriasTransactionsFormatterXML.xml";

                Console.WriteLine("Carregando arquivo json de origem...");
                Console.WriteLine();
                var jsonString = File.ReadAllText(fileNameSource);

                var anaSete = JsonSerializer.Deserialize<AnaSete>(jsonString);

                if (anaSete == null || anaSete.issues == null)
                    throw new Exception("Objects is null");

                Console.WriteLine("Preparando históricos das estórias...");
                Console.WriteLine();
                var issuesResultList = GetIssuesResult(anaSete).Where(x => x.IssuesResultHistories.Count() > 0).OrderBy(x => x.Sprint);

                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                };
                            
                Console.WriteLine("Salvando arquivos json/xml com históricos de estórias formatadas...");
                Console.WriteLine();
                var issuesResultListJson = JsonSerializer.Serialize(issuesResultList, jsonSerializerOptions);
                File.WriteAllText(fileNameJsonClose, issuesResultListJson);

                var streamFileXml = new FileStream(fileNameXmlClose, FileMode.Create);
                            
                var issuesResultListXml = new XmlSerializer(issuesResultList.ToList().GetType());
                issuesResultListXml.Serialize(streamFileXml, issuesResultList.ToList());
                streamFileXml.Close();
    
                Console.WriteLine();
                Console.WriteLine("Seus arquivos estão pronto para uso, pressione qualquer tecla para sair do app!!!");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
 
        }

        private static List<IssuesResult> GetIssuesResult(AnaSete anaSete)
        {
            var issuesResultList = new List<IssuesResult>();  

            foreach(var itemIssues in anaSete.issues.OrderBy(x => x.id))
            {
                var issuesResult = new IssuesResult
                {
                    Id = itemIssues?.id,
                    Key = itemIssues?.key,
                    Summary = itemIssues?.fields?.summary,
                };

                if (itemIssues == null)
                    continue;


                string dateChangeStatusOld = "0";
                foreach(var itemHistories in itemIssues.changelog.histories.OrderBy(x => x.created))
                {
                    if (string.IsNullOrEmpty(issuesResult.Sprint))
                        issuesResult.Sprint = itemHistories?.items?.FirstOrDefault(x => x.field == "Sprint")?.toString;    

                    var itemsStatus = itemHistories?.items.Where(x => x.field == "status" && x.fromString != x.toString);
                    if (itemsStatus == null || itemsStatus.Count() == 0)
                        continue;

                    var dateChangeStatus = DateTime.SpecifyKind(Convert.ToDateTime(itemHistories?.created), DateTimeKind.Utc);
                    var dateFrom = (dateChangeStatusOld != "0") ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;
                    var dateTo = Convert.ToDateTime(dateChangeStatus.ToString("yyyy-MM-dd"));
            
                    var issuesResultHistories = new IssuesResultHistories
                    {
                        UserKey = itemHistories?.author?.name,
                        UserName = itemHistories?.author?.displayName,
                        DateChangeStatus = dateChangeStatus.ToString("yyyy-MM-dd"),
                        CycleTime = (dateFrom != DateTime.MinValue) ? (int)dateTo.Subtract(dateFrom).TotalDays : 0,
                        CycleTimeWorkDays = (dateFrom != DateTime.MinValue) ? GetWorkingDays(dateFrom, dateTo) : 0
                    };

                    foreach (var items in itemsStatus)
                    {
                        issuesResultHistories.FromStatus = items.fromString;
                        issuesResultHistories.ToStatus = items.toString;
                    }

                    issuesResult.IssuesResultHistories.Add(issuesResultHistories);
                        
                    dateChangeStatusOld = dateChangeStatus.ToString("yyyy-MM-dd");

                }
                issuesResultList.Add(issuesResult);
            }
            return issuesResultList;
        }

        private static int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
        {
            var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
            return Enumerable
                    .Range(1, dayDifference)
                    .Select(x => dateFrom.AddDays(x))
                .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
        }
    }
}
