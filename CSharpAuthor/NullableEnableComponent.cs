namespace CSharpAuthor;

public class NullableEnableComponent : BaseOutputComponent {
    private bool _enable;

    public NullableEnableComponent(bool enable) {
        _enable = enable;
    }

    protected override void WriteComponentOutput(IOutputContext outputContext) {
        var enableString = _enable ? "enable" : "disable";
        outputContext.WriteIndentedLine($"#nullable {enableString}");
    }
}