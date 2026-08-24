using System;
using LaPrimitiva.Domain.Models;

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
                if (_selectedYear != value || _showAllPlans)
                {
                    _selectedYear = value;
                    _showAllPlans = false;
                    NotifyStateChanged();
                    NotifyDataChanged();
                }
            }
        }

        private bool _showAllPlans;
        public bool ShowAllPlans
        {
            get => _showAllPlans;
            set
            {
                if (_showAllPlans != value)
                {
                    _showAllPlans = value;
                    NotifyStateChanged();
                    NotifyDataChanged();
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
                    NotifyDataChanged();
                }
            }
        }

        private bool _isHistoricalView;
        public bool IsHistoricalView
        {
            get => _isHistoricalView;
            set
            {
                if (_isHistoricalView != value)
                {
                    _isHistoricalView = value;
                    NotifyStateChanged();
                    NotifyDataChanged();
                }
            }
        }

        private int _newDrawsCount;
        public int NewDrawsCount
        {
            get => _newDrawsCount;
            set
            {
                if (_newDrawsCount != value)
                {
                    _newDrawsCount = value;
                    NotifyStateChanged();
                }
            }
        }

        private List<RssDraw> _recentDraws = new();
        public List<RssDraw> RecentDraws
        {
            get => _recentDraws;
            set
            {
                _recentDraws = value;
                NotifyStateChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; NotifyStateChanged(); } }
        }

        private string? _lastError;
        public string? LastError
        {
            get => _lastError;
            set { if (_lastError != value) { _lastError = value; NotifyStateChanged(); } }
        }

        public bool HasNewDraws => NewDrawsCount > 0;

        public event Action? OnChange;
        public event Action? OnDataRefreshRequired;

        public void NotifyStateChanged() => OnChange?.Invoke();
        public void NotifyDataChanged() => OnDataRefreshRequired?.Invoke();
    }
}
