using static IssuesJiraModel;

public class FormatterIssuesJira
{
    private static int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
    {
        var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
        return Enumerable
            .Range(1, dayDifference)
            .Select(x => dateFrom.AddDays(x))
            .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
        }

    private static List<string> GetSprintsIssues(IOrderedEnumerable<Histories> itemIssuesChangelogHistories)
    {
        var sprintList = new List<string>();
        itemIssuesChangelogHistories.ToList().ForEach(x =>
        {
            var sprintsIssue = x.items.Where(x => x.field == "Sprint" && !string.IsNullOrEmpty(x.toString)).ToList();
            sprintsIssue.ForEach(x => 
            {
                var sprintName = !string.IsNullOrEmpty(x.toString) ? x.toString : string.Empty;
                if (sprintList.IndexOf(sprintName) == -1)   
                    sprintList.Add(sprintName);
            });
        });

        return sprintList;
    }

    public List<IssuesResult> GetIssuesResult(IssuesJira issuesJira)
    {
        var issuesResultList = new List<IssuesResult>();  

        foreach(var itemIssues in issuesJira.issues.OrderBy(x => x.id))
        {
            var issuesResult = new IssuesResult
            {
                Id = itemIssues?.id,
                Key = itemIssues?.key,
                Summary = itemIssues?.fields?.summary,
            };

            if (itemIssues == null)
                continue;

            var itemIssuesChangelogHistories = itemIssues.changelog.histories.OrderBy(x => x.created);
            var sprintList = GetSprintsIssues(itemIssuesChangelogHistories);
            issuesResult.Sprint = sprintList?.LastOrDefault();
            issuesResult.Replanning = sprintList?.Count > 1 ? "Sim" : "Não";
            sprintList?.ForEach(x =>
            {
                issuesResult.HistorySprint = string.IsNullOrEmpty(issuesResult.HistorySprint) ? x : issuesResult.HistorySprint + " / " + x;
            });

            string dateChangeStatusOld = "0";
            foreach (var itemHistories in itemIssuesChangelogHistories)
            {
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

}