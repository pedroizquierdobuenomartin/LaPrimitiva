using System;

namespace LaPrimitiva.Application.Services
{
    public class GlobalState
    {
        private int _selectedYear = DateTime.Now.Year;
        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (_selectedYear != value)
                {
                    _selectedYear = value;
                    NotifyStateChanged();
                }
            }
        }

        private Guid? _selectedPlanId;
        public Guid? SelectedPlanId
        {
            get => _selectedPlanId;
            set
            {
                if (_selectedPlanId != value)
                {
                    _selectedPlanId = value;
                    NotifyStateChanged();
                }
            }
        }

        public event Action? OnChange;

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
