using static IssuesJiraModel;

public class FormatterIssuesJira
{
    const string _YES = "Sim";
    const string _NO = "Não";
    const string _STATUS = "status";
    const string _SPRINT = "Sprint";
    const string _BACKLOG = "Backlog";
    const string _PLANNED = "Planejado";
    const string _FORMATDATE = "yyyy-MM-dd";

    // Return diff dates just work days
    private static int GetWorkingDays(DateTime dateFrom, DateTime dateTo)
    {
        var dayDifference = (int)dateTo.Subtract(dateFrom).TotalDays;
        return Enumerable
            .Range(1, dayDifference)
            .Select(x => dateFrom.AddDays(x))
            .Count(x => x.DayOfWeek != DayOfWeek.Saturday && x.DayOfWeek != DayOfWeek.Sunday);
    }

    // Return sprint list 
    private static List<string> GetSprintsIssues(IOrderedEnumerable<Histories> itemIssuesChangelogHistories)
    {
        var sprintList = new List<string>();
        var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString)));

        itemIssuesChangelogHistoriesFiltered.ToList().ForEach(x =>
        {
            var sprintIssue = x.items.FirstOrDefault(x => x.field == _SPRINT && !string.IsNullOrEmpty(x.toString));
            var sprintName = sprintIssue?.toString;
            if (!string.IsNullOrEmpty(sprintName) && sprintList.IndexOf(sprintName) == -1)   
                sprintList.Add(sprintName);

        });

        return sprintList;
    }

    // Return real date that issue developing in sprint
    private static DateTime? GetDateAfterReplanning(IOrderedEnumerable<Histories> itemIssuesChangelogHistories, string replanning)
    {
        if (replanning != _YES)
            return null;

        var dateStatusReplanning = itemIssuesChangelogHistories.LastOrDefault(x => x.items.Any(x => x.field == _STATUS && x.fromString != _BACKLOG && x.fromString == x.toString))?.created;

        return !string.IsNullOrEmpty(dateStatusReplanning) ? DateTime.SpecifyKind(Convert.ToDateTime(dateStatusReplanning), DateTimeKind.Utc) : null;
    }

    // Build object with history status
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

            // Get sprints by issue and check if had replanning
            var itemIssuesChangelogHistories = itemIssues.changelog.histories.OrderBy(x => x.created);
            var sprintList = GetSprintsIssues(itemIssuesChangelogHistories);
            issuesResult.Sprint = sprintList?.LastOrDefault();
            issuesResult.Replanning = sprintList?.Count > 1 ? _YES : _NO;
            sprintList?.ForEach(x =>
            {
                issuesResult.HistorySprint = string.IsNullOrEmpty(issuesResult.HistorySprint) ? x : issuesResult.HistorySprint + " / " + x;
            });

            // Get date replanning otherwise real date issue devlop in sprint
            var dateStatusAfterReplanning = GetDateAfterReplanning(itemIssuesChangelogHistories, issuesResult.Replanning);
            issuesResult.DateReplanning = dateStatusAfterReplanning.HasValue ? dateStatusAfterReplanning.Value.ToString(_FORMATDATE) : string.Empty;

            string dateChangeStatusOld = "0";
            var itemIssuesChangelogHistoriesFiltered = itemIssuesChangelogHistories.Where(x => x.items.Any(x => x.field == _STATUS && x.fromString != x.toString));

            // Build history status issue and cycletimes
            foreach (var itemHistories in itemIssuesChangelogHistoriesFiltered)
            {

                var itemsStatus = itemHistories?.items.Where(x => x.field == _STATUS && x.fromString != x.toString);
                if (itemsStatus == null || itemsStatus.Count() == 0)
                    continue;

                // Prepare dates to calculate cycletimes
                var dateChangeStatus = DateTime.SpecifyKind(Convert.ToDateTime(itemHistories?.created), DateTimeKind.Utc);
                var dateFrom = (dateChangeStatusOld != "0") ? Convert.ToDateTime(dateChangeStatusOld) : DateTime.MinValue;
                var dateTo = Convert.ToDateTime(dateChangeStatus.ToString(_FORMATDATE));

                var dateChangeStatusAfterReplanning = dateStatusAfterReplanning.HasValue ? dateStatusAfterReplanning.Value : dateChangeStatus;
                var dateToAfterReplanning = Convert.ToDateTime(dateChangeStatusAfterReplanning.ToString(_FORMATDATE));

                var issuesResultHistories = new IssuesResultHistories
                {

                    UserKey = itemHistories?.author?.name,
                    UserName = itemHistories?.author?.displayName,

                    DateChangeStatus = dateChangeStatus.ToString(_FORMATDATE),
                    CycleTime = (dateFrom != DateTime.MinValue) ? (int)dateTo.Subtract(dateFrom).TotalDays : 0,
                    CycleTimeWorkDays = (dateFrom != DateTime.MinValue) ? GetWorkingDays(dateFrom, dateTo) : 0,

                    CycleTimeAfterReplanning = (dateFrom != DateTime.MinValue) ? (int)dateToAfterReplanning.Subtract(dateFrom).TotalDays : 0,
                    CycleTimeWorkDaysAfterReplanning = (dateFrom != DateTime.MinValue) ? GetWorkingDays(dateFrom, dateToAfterReplanning) : 0,

                };

                foreach (var items in itemsStatus)
                {
                    issuesResultHistories.FromStatus = items.fromString;
                    issuesResultHistories.ToStatus = items.toString;
                }

                dateStatusAfterReplanning = (dateStatusAfterReplanning.HasValue && issuesResultHistories.ToStatus == _PLANNED) ? dateStatusAfterReplanning.Value : null;

                issuesResult.IssuesResultHistories.Add(issuesResultHistories);

                dateChangeStatusOld = dateChangeStatus.ToString(_FORMATDATE);
            }

            issuesResultList.Add(issuesResult);
        }
        return issuesResultList;
    }

}