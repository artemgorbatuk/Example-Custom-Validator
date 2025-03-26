using Custom_Validator;
using System.Diagnostics;

namespace Tests;

public class Order
{
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public string Currency { get; set; } = default!;
    public bool IsDiscounted { get; set; }
    public decimal Discount { get; set; }
}

public class OrderValidator : ValidatorBase<Order>
{
    public OrderValidator()
    {
        AddRule(o => o.Price, o => o.Price <= 0, "Цена должна быть больше 0");
        AddRule(o => o.Quantity, o => o.Quantity <= 0, "Количество должно быть больше 0");
        AddRule(o => o.Currency, o => string.IsNullOrEmpty(o.Currency) || string.IsNullOrWhiteSpace(o.Currency), "Валюта не указана");
        AddRule(o => o.Currency, o => !string.IsNullOrWhiteSpace(o.Currency) && o.Currency?.Length != 3, "Валюта должна быть 3 символа");
        AddRule(o => o.Discount, o => o.Discount < 0, "Скидка не может быть отрицательной");
        AddRule(o => o.Discount, o => !o.IsDiscounted && o.Discount != 0, "Скидка должна быть равена 0");
        AddRule(o => o.Discount, o => o.IsDiscounted && o.Discount >= o.Price, "Скидка не может быть равна цене или превышать цену");
    }

    protected override void ValidateModel(Order order)
    {
        // Сложная проверка 1: Если скидка применена, но сумма скидки нулевая
        if (order.IsDiscounted && order.Discount == 0)
        {
            AddError(nameof(Order.Discount),
                "Для активированной скидки укажите сумму");
        }

        // Сложная проверка 2: Специальная валидация для VIP-заказов
        if (order.Price < 50 && order.Quantity < 10)
        {
            AddError(nameof(order.Quantity), "Заказы ценой менее 50 должны включать 10 и более единиц");
        }

        // Сложная проверка 3: добавляем проверки через стратегии
        AddStrategy(new OrderVIPStrategy());
    }
}

public class OrderVIPStrategy : IValidationStrategy<Order>
{
    public IEnumerable<ValidationRule<Order>> SetRules()
    {
        yield return new ValidationRule<Order>
        {
            PropertyName = nameof(Order.Price),
            Condition = o => o.Price > 50_000,
            ErrorMessage = "Для VIP-заказов требуется специальный код"
        };
    }
}

public class OrderWeekendStrategy : IValidationStrategy<Order>
{

    private DayOfWeek currentDay = DayOfWeek.Saturday;
    private bool IsWeekend => currentDay is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public IEnumerable<ValidationRule<Order>> SetRules()
    {
        yield return new ValidationRule<Order>
        {
            PropertyName = nameof(Order.Quantity),
            Condition = o => IsWeekend && o.Price > 10_000 && o.Quantity > 2,
            ErrorMessage = "В выходные максимум 2 товар дороже 10 000"
        };
        yield return new ValidationRule<Order>
        {
            PropertyName = nameof(Order.Quantity),
            Condition = o => IsWeekend && o.Price > 20_000 && o.Quantity > 1,
            ErrorMessage = "В выходные максимум 1 товар дороже 20 000"
        };
    }
}


public class OrderValidatorTests
{
    [Theory]

    // Основные сценарии
    [InlineData("001", 100, 1, "USD", false, 0, true, 0)]             // Валидный заказ без скидки
    [InlineData("002", 100, 1, "USD", true, 10, true, 0)]             // Валидный заказ со скидкой
    [InlineData("003", 100, 1, "USD", true, 0, false, 1)]              // Скидка 0 при isDiscounted=true
    [InlineData("004", 100, 1, "USD", true, 100, false, 1)]           // Скидка равна цене
    [InlineData("005", 100, 1, "USD", true, 101, false, 1)]           // Скидка больше цены
    [InlineData("006", 0, 1, "USD", false, 0, false, 3)]              // Нулевая цена
    [InlineData("007", -50, 1, "USD", false, 0, false, 3)]            // Отрицательная цена
    [InlineData("008", 100, 0, "USD", false, 0, false, 1)]            // Нулевое количество
    [InlineData("009", 100, -1, "USD", false, 0, false, 1)]           // Отрицательное количество
    [InlineData("010", 100, 1, null, false, 0, false, 1)]             // Пустая валюта
    [InlineData("011", 100, 1, "", false, 0, false, 1)]               // Пустая строка валюты
    [InlineData("012", 100, 1, "   ", false, 0, false, 1)]            // Пробелы в валюте

    // Граничные значения
    [InlineData("013", 0.01, 1, "EUR", false, 0, false, 2)]           // Минимальная цена
    [InlineData("014", int.MaxValue, 1, "GBP", false, 0, true, 0)]    // Максимальные значения
    [InlineData("015", 100, int.MaxValue, "JPY", false, 0, true, 0)]  // Максимальные значения
    [InlineData("016", 100, 1, "RUB", true, 99.99, true, 0)]          // Скидка чуть меньше цены

    // Специальные кейсы
    [InlineData("017", 100, 1, "USD", false, 50, false, 1)]           // Скидка указана, но isDiscounted=false
    [InlineData("018", 100, 1, "USD", true, -5, false, 1)]            // Отрицательная скидка
    [InlineData("019", 100, 1, "LONG_CURRENCY", false, 0, false, 1)]  // Длинный код валюты
    [InlineData("020", 0, 0, "LONG_CURRENCY", false, 10, false, 6)]   // 5 ошибок
    [InlineData("021", 40, 5, "RUB", false, 0, false, 1)]             // ValidateModel => сложная проверка
    [InlineData("022", 10, 4, "RUB", false, 0, false, 2)]             // Правило добавлено после инициализации
    [InlineData("023", 20000, 4, "RUB", false, 0, false, 1)]          // SetupWeekendRules правило
    [InlineData("023", 20000, 1, "RUB", false, 0, true, 0)]          // SetupWeekendRules правило

    public void OrderValidatorTest(string testCase,decimal price, int quantity, string currency, bool isDiscounted, decimal discount, bool expectedIsValid, int countErrors)
    {
        Debug.Print(testCase);

        var order = new Order
        {
            Price = price,
            Quantity = quantity,
            Currency = currency,
            IsDiscounted = isDiscounted,
            Discount = discount
        };

        var validator = new OrderValidator();

        validator.AddRule(
            o => o.Price, 
            o => o.Price < 25 && o.Quantity < 5, 
            "Заказы ценой менее 25 должны включать 5 и более единиц");

        validator.AddStrategy(new OrderWeekendStrategy());

        var result = validator.Validate(order);

        Assert.Equal(expectedIsValid, result.IsValid);
        Assert.Equal(countErrors, result.Errors.Count);
    }
}