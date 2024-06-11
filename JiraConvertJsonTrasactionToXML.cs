using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Serialization;
using Newtonsoft.Json;
using static IssuesJiraModel;
using static FormatterIssuesJiraCommon;

namespace JiraConvertJsonTrasactionToXML
{
   
    public class JiraConvertJsonTrasactionToXML
    {
        public static void Main()
        {
            try
            {
                var baseDirecotry = AppDomain.CurrentDomain.BaseDirectory;
                var fileNameSource = baseDirecotry + "SourceEstoriasJiraAPI.json";
                var fileNameJsonClose = baseDirecotry + "EstoriasTransactionsFormatterJSON.json";
                var fileNameXmlClose = baseDirecotry + "EstoriasTransactionsFormatterXML.xml";

                Console.WriteLine(_LOADING_SOURCE_JSON);
                Console.WriteLine();

                var jsonString = File.ReadAllText(fileNameSource);

                var jsonSerializerOptions = new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true,
                };

                jsonString = jsonString.Replace(_START_FROM_CUSTOM_FIELD, _START_TO_CUSTOM_FIELD).Replace(_END_FROM_CUSTOM_FIELD, _END_TO_CUSTOM_FIELD);

                //jsonString = jsonString.Replace(_START_FROM_CUSTOM_FIELD, _START_TO_CUSTOM_FIELD).Replace("autoStartStop=false]\"\r\n                ]", "autoStartStop=false]\"");   

                var issuesJira = JsonConvert.DeserializeObject<IssuesJira>(jsonString);

                if (issuesJira == null || issuesJira.issues == null)
                    throw new Exception(_OBJECT_IS_NULL);

                Console.WriteLine(_PREPARE_HISTORY_ISSUES);
                Console.WriteLine();
                var issuesResultList = new FormatterIssuesJira().GetIssuesResult(issuesJira).Where(x => x.IssuesResultHistories.Count() > 0).OrderBy(x => x.Sprint);

                Console.WriteLine(_SAVING_JSON_XML_FILE_HISTORY_ISSUES);
                Console.WriteLine();
                var issuesResultListJson = JsonConvert.SerializeObject(issuesResultList);
                File.WriteAllText(fileNameJsonClose, issuesResultListJson);

                var streamFileXml = new FileStream(fileNameXmlClose, FileMode.Create);
                            
                var issuesResultListXml = new XmlSerializer(issuesResultList.ToList().GetType());
                issuesResultListXml.Serialize(streamFileXml, issuesResultList.ToList());
                streamFileXml.Close();
    
                Console.WriteLine();
                Console.WriteLine(_FILES_READY_TO_USE_PRESS_ANY_TYPE_EXIT);
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
