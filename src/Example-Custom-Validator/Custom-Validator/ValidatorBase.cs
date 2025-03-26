using System;
using System.Linq.Expressions;

namespace Custom_Validator;

public interface IValidator<TObj>
{
    // Основные методы
    IValidator<TObj> AddRule(string propertyName, Func<TObj, bool> condition, string errorMessage);

    // Перегрузки с лямбда-выбором свойства
    IValidator<TObj> AddRule<TProp>(Expression<Func<TObj, TProp>> propertySelector, Func<TObj, bool> condition, string errorMessage);

    // Валидация
    ValidationResult Validate(TObj model);
}

public interface IValidationStrategy<TObj>
{
    IEnumerable<ValidationRule<TObj>> SetRules();
}

public abstract class ValidatorBase<T> : IValidator<T>
{
    protected ValidationResult Result { get; private set; } = new();
    private readonly List<ValidationRule<T>> _rules = [];

    public ValidationResult Validate(T model)
    {
        Result = new ValidationResult();
        ApplyRules(model);
        ValidateModel(model);
        return Result;
    }

    protected void AddError(string propertyName, string message)
    {
        Result.Errors.Add(new ValidationModels { PropertyName = propertyName, ErrorMessage = message });
    }

    #region Правила : основные методы

    public IValidator<T> AddRule(string propertyName, Func<T, bool> condition, string errorMessage)
    {
        _rules.Add(new ValidationRule<T>
        {
            PropertyName = propertyName,
            Condition = condition,
            ErrorMessage = errorMessage
        });
        return this;
    }

    public IValidator<T> AddStrategy(IValidationStrategy<T> strategy)
    {
        _rules.AddRange(strategy.SetRules());

        return this;
    }

    #endregion

    #region Правила : перегрузки с лямбда-выбором свойства

    public IValidator<T> AddRule<TProp>(Expression<Func<T, TProp>> propertySelector, Func<T, bool> condition, string errorMessage)
    {
        return AddRule(GetPropertyName(propertySelector), condition, errorMessage);
    }

    #endregion

    #region Вспомогательный методы

    private static string GetPropertyName<TProp>(Expression<Func<T, TProp>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
            return memberExpression.Member.Name;

        throw new ArgumentException("Expression must be a property", nameof(expression));
    }

    private void ApplyRules(T model)
    {
        foreach (var rule in _rules)
        {
            if (rule.Condition(model))
            {
                AddError(rule.PropertyName, rule.ErrorMessage);
            }
        }
    }

    protected virtual void ValidateModel(T model) { }

    #endregion
}