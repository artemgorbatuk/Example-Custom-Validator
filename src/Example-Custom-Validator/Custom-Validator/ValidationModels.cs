namespace Custom_Validator;

public class ValidationRule<T>
{
    public Func<T, bool> Condition { get; set; } = default!;
    public string PropertyName { get; set; } = default!;
    public string ErrorMessage { get; set; } = default!;
}

public class ValidationResult
{

    public bool IsValid => Errors.Count == 0;
    public List<ValidationModels> Errors { get; } = [];
}

public class ValidationModels
{
    public string PropertyName { get; set; } = default!;
    public string ErrorMessage { get; set; } = default!;
}