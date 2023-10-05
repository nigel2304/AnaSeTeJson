using System.Data;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Serialization;
using Newtonsoft.Json;
using static IssuesJiraModel;

namespace JiraConvertJsonTrasactionToXML
{
   
    public class JiraConvertJsonTrasactionToXML
    {
        public static void Main()
        {
            try
            {
                const string _STARTFROMCUSTOMFIELD = "\"customfield_10105\": [\r\n";
                const string _STARTTOCUSTOMFIELD = "\"customfield_10105\":";

                const string _ENDFROMCUSTOMFIELD = "\r\n                ],\r\n                \"issuetype\"";
                const string _ENDTOCUSTOMFIELD = ",\r\n                \"issuetype\"";

                var baseDirecotry = AppDomain.CurrentDomain.BaseDirectory;
                var fileNameSource = baseDirecotry + "SourceEstoriasJiraAPI.json";
                var fileNameJsonClose = baseDirecotry + "EstoriasTransactionsFormatterJSON.json";
                var fileNameXmlClose = baseDirecotry + "EstoriasTransactionsFormatterXML.xml";

                Console.WriteLine("Carregando arquivo json de origem...");
                Console.WriteLine();

                var jsonString = File.ReadAllText(fileNameSource);

                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                };

                jsonString = jsonString.Replace(_STARTFROMCUSTOMFIELD, _STARTTOCUSTOMFIELD).Replace(_ENDFROMCUSTOMFIELD, _ENDTOCUSTOMFIELD);

                var issuesJira = JsonConvert.DeserializeObject<IssuesJira>(jsonString);

                if (issuesJira == null || issuesJira.issues == null)
                    throw new Exception("Objects is null");

                Console.WriteLine("Preparando históricos das estórias...");
                Console.WriteLine();
                var issuesResultList = new FormatterIssuesJira().GetIssuesResult(issuesJira).Where(x => x.IssuesResultHistories.Count() > 0).OrderBy(x => x.Sprint);

                Console.WriteLine("Salvando arquivos json/xml com históricos de estórias formatadas...");
                Console.WriteLine();
                var issuesResultListJson = JsonConvert.SerializeObject(issuesResultList);
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
    }
}
