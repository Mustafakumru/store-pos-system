using LCWaikiki.POS.WpfClient.ViewModels;

namespace LCWaikiki.POS.WpfClient.Tests;

/// <summary>İlk girişte yeni PIN: sunucuyla aynı kural ilk adımda uygulanır.</summary>
public class LoginPinRuleTests
{
    [Theory]
    [InlineData("1234", true)]
    [InlineData("4321", true)]
    [InlineData("1111", true)]
    [InlineData("456789", true)]
    [InlineData("4826", false)]
    [InlineData("1235", false)]
    [InlineData("9137", false)]
    public void Guessable_pins_are_detected(string pin, bool guessable)
        => Assert.Equal(guessable, LoginViewModel.IsGuessablePin(pin));
}
