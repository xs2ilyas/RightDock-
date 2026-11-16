using System.Windows.Media;
using RightDock.Data;

namespace RightDock.Modules.Medication
{
    internal static class MedicationFormat
    {
        internal static readonly Brush TakenBrush = ModuleUtil.Brush("#8FD3A8");
        internal static readonly Brush MissedBrush = ModuleUtil.Brush("#EF8080");
        internal static readonly Brush SkippedBrush = ModuleUtil.Brush("#C9A36B");
        internal static readonly Brush PendingBrush = ModuleUtil.Brush("#8C8C8C");

        internal static string StatusLabel(MedicationDose dose)
        {
            switch (dose.Status)
            {
                case "taken":
                    return dose.TakenUtc.HasValue
                        ? "Taken " + dose.TakenUtc.Value.ToLocalTime().ToString("HH:mm")
                        : "Taken";
                case "skipped":
                    return "Skipped";
                case "missed":
                    return "Missed";
                case "snoozed":
                    return "Snoozed · " + dose.TimeLabel;
                default:
                    return dose.IsOverdue ? "Overdue" : "Upcoming";
            }
        }

        internal static Brush StatusBrush(MedicationDose dose)
        {
            switch (dose.Status)
            {
                case "taken": return TakenBrush;
                case "skipped": return SkippedBrush;
                case "missed": return MissedBrush;
                case "snoozed": return SkippedBrush;
                default: return dose.IsOverdue ? MissedBrush : PendingBrush;
            }
        }
    }
}
