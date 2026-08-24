using LaPrimitiva.Application.Services;
using Xunit;

namespace LaPrimitiva.Tests
{
    public class GlobalStateTests
    {
        [Fact]
        public void ShowAllPlans_ShouldPreserveSelectedYear()
        {
            var state = new GlobalState();
            var selectedYear = state.SelectedYear;

            state.ShowAllPlans = true;

            Assert.True(state.ShowAllPlans);
            Assert.Equal(selectedYear, state.SelectedYear);
        }

        [Fact]
        public void SelectingYear_ShouldExitShowAllPlansMode_AndNotifyOnce()
        {
            var state = new GlobalState
            {
                ShowAllPlans = true
            };
            var stateChanges = 0;
            var dataChanges = 0;
            state.OnChange += () => stateChanges++;
            state.OnDataRefreshRequired += () => dataChanges++;

            state.SelectedYear = state.SelectedYear;

            Assert.False(state.ShowAllPlans);
            Assert.Equal(1, stateChanges);
            Assert.Equal(1, dataChanges);
        }
    }
}
