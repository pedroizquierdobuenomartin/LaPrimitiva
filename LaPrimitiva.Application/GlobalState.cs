using System;

namespace LaPrimitiva.Application.Services
{
    public class GlobalState
    {
        public int SelectedYear { get; set; } = DateTime.Now.Year;
        public Guid? SelectedPlanId { get; set; }

        public event Action? OnChange;

        public void SetYear(int year)
        {
            SelectedYear = year;
            NotifyStateChanged();
        }

        public void SetPlan(Guid? planId)
        {
            SelectedPlanId = planId;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
