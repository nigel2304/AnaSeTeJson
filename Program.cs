using System.Data;
using System.Reflection;
using System.Text.Json;
using Excel = Microsoft.Office.Interop.Excel;


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
        public List<IssuesResultHistories> IssuesResultHistories { get; set; } = new List<IssuesResultHistories>();
    }

    public class IssuesResultHistories
    {
        public string? UserKey { get; set; }
        public string? UserName { get; set; }
        public DateTime? DateChangeStatus { get; set; }
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
    }


    public class Program
    {
        public static void Main()
        {
            var fileName = "C:\\MSProjects\\AnaSeTeJson\\EstoriasAnaSeTe.json";
            var jsonString = File.ReadAllText(fileName);

            var anaSete = JsonSerializer.Deserialize<AnaSete>(jsonString);

            if (anaSete == null || anaSete.issues == null)
                throw new Exception("Objects is null");

            var issuesResultList = GetIssuesResult(anaSete);

            foreach (var itemsResult in issuesResultList)
            {
                Console.WriteLine($"Id: {itemsResult?.Id}");
                Console.WriteLine($"Key: {itemsResult?.Key}");
                Console.WriteLine($"Summary: {itemsResult?.Summary}");
                
                if (itemsResult == null)
                    continue;

                foreach (var itemsResultHistories in itemsResult.IssuesResultHistories)
                {
                    Console.WriteLine($"User Key: {itemsResultHistories?.UserKey}");
                    Console.WriteLine($"User Name: {itemsResultHistories?.UserName}");
                    Console.WriteLine($"Date Change Status: {itemsResultHistories?.DateChangeStatus}");
                    Console.WriteLine($"From Status: {itemsResultHistories?.FromStatus}");
                    Console.WriteLine($"To Status: {itemsResultHistories?.ToStatus}");

                    Console.WriteLine();
                }
            }

            var dataTable = ToDataTable(issuesResultList);

            GenerateExcel(dataTable);
        }

        private static List<IssuesResult> GetIssuesResult(AnaSete anaSete)
        {
            var issuesResultList = new List<IssuesResult>();  

            foreach(var itemIssues in anaSete.issues.Where(x => x.key == "ANAEXPRES-104"))
            {
                var issuesResult = new IssuesResult
                {
                    Id = itemIssues?.id,
                    Key = itemIssues?.key,
                    Summary = itemIssues?.fields?.summary
                };

                if (itemIssues == null)
                    continue;

                foreach(var itemHistories in itemIssues.changelog.histories.OrderBy(x => x.created))
                {
                    var itemsStatus = itemHistories?.items.Where(x => x.field == "status" && x.fromString != x.toString);
                    if (itemsStatus == null || !itemsStatus.Any())
                        continue;

                    var issuesResultHistories = new IssuesResultHistories
                    {
                        UserKey = itemHistories?.author?.name,
                        UserName = itemHistories?.author?.displayName,
                        DateChangeStatus = Convert.ToDateTime(itemHistories?.created),
                    };

                    foreach (var items in itemsStatus)
                    {
                        issuesResultHistories.FromStatus = items.fromString;
                        issuesResultHistories.ToStatus = items.toString;
                    }

                    issuesResult.IssuesResultHistories.Add(issuesResultHistories);
                }
                issuesResultList.Add(issuesResult);
            }
            return issuesResultList;
        }  

        private static DataTable ToDataTable(List<IssuesResult> issuesResult)
        {
            // Creating a data table instance and typed it as our incoming model as I make it generic, if you want, you can make it the model typed you want.            
            DataTable dataTable  = new DataTable(typeof(IssuesResult).Name);

            //Get all the properties of that model
            PropertyInfo[] Props = typeof(IssuesResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);  
            
            //Get all the properties of that model adding Column name to our datatable 
            foreach (PropertyInfo prop in Props)  
            {  
                //Setting column names as Property names    
                dataTable.Columns.Add(prop.Name);  
            }

            // Adding Row and its value to our dataTable  
            foreach (var itemIssuesResult in issuesResult)  
            {  
                var values = new object[Props.Length];  

                // Inserting property values to datatable rows 
                for (int i = 0; i < Props.Length; i++)  
                    values[i] = Props[i].GetValue(itemIssuesResult, null);  
                
                // Finally add value to datatable  
                dataTable.Rows.Add(values);  
            }  
            return dataTable;  
        }        

        public static void GenerateExcel(DataTable dataTable)  
        {  
            var pathFileName = "C:\\MSProjects\\AnaSeTeJson\\EstoriasAnaSeTe.xlsx";

            DataSet dataSet = new DataSet();  
            dataSet.Tables.Add(dataTable);  
        
            // Create a excel app along side with workbook and worksheet and give a name to it  
            Excel.Application excelApp = new Excel.Application();  
            Excel.Workbook excelWorkBook = excelApp.Workbooks.Add();  
            Excel._Worksheet xlWorksheet = (Excel._Worksheet)excelWorkBook.Sheets[1];  
            Excel.Range xlRange = xlWorksheet.UsedRange;  

            foreach (DataTable table in dataSet.Tables)  
            {  
                //Add a new worksheet to workbook with the Datatable name  
                Excel.Worksheet excelWorkSheet = (Excel.Worksheet)excelWorkBook.Sheets.Add();  
                excelWorkSheet.Name = table.TableName;  
        
                // Add all the columns  
                for (int i = 1; i < table.Columns.Count + 1; i++)  
                {  
                    excelWorkSheet.Cells[1, i] = table.Columns[i - 1].ColumnName;  
                }  
        
                // Add all the rows  
                for (int j = 0; j < table.Rows.Count; j++)  
                {  
                    for (int k = 0; k < table.Columns.Count; k++)  
                    {  
                        excelWorkSheet.Cells[j + 2, k + 1] = table.Rows[j].ItemArray[k].ToString();  
                    }  
                }  
            }   

            excelWorkBook.SaveAs(pathFileName);
            excelWorkBook.Close();  
            excelApp.Quit();        
        }
    }
}
