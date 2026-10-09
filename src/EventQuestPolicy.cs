namespace EquinoxCompanion;
// Small independent snapshots keep new festival quests below the upload size limit.
public static class EventQuestPolicy
{
    private static bool Text(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length<=100 && !s.Any(char.IsControl);
    public static bool Valid(CollectionDetails c) =>
        c.Segment is >=0 and <=1000 &&
        (c.QuestDefinitions is null || c.Category=="quest" && c.QuestDefinitions.Length<=8 &&
          c.QuestDefinitions.Select(q=>q.Id).Distinct().Count()==c.QuestDefinitions.Length &&
          c.QuestDefinitions.All(q=>c.Known.Contains(q.Id)&&Text(q.Name)&&q.FestivalId is >0 and <1000000&&Text(q.FestivalName)&&
            q.MinLevel is >=0 and <=200 && q.PreviousQuests.Length<=3&&q.PreviousQuests.All(id=>id is >0 and <1000000)&&
            q.Rewards.Length<=12&&q.Rewards.All(r=>r.ItemId is >0 and <1000000&&Text(r.Name)))) &&
        (c.Accepted is null || c.Category=="quest" && c.Accepted.Length<=5000 && c.Accepted.All(c.Known.Contains));
}
