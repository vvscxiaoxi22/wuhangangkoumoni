
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 浮点数速度转颜色转换器：岸桥三轴共用
/// speed>0 →绿色；speed<0→红色；接近0灰色
/// </summary>
public static class CraneSpeedToColorConverter
{
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
    public static void RegisterConverter()
    {
        // 创建转换器组，名字后面UI Builder下拉框能看到
        var group = new ConverterGroup("BoolToColor");

        // float → StyleColor
        group.AddConverter((ref bool source) =>
        {
            StyleColor c;
            if (source == true)
            {
                ColorUtility.TryParseHtmlString("#4696FF", out Color col);
                c = new StyleColor(col);
            }
            else
            {
                ColorUtility.TryParseHtmlString("#6E7D91", out Color col);
                c = new StyleColor(col);
            }
            return c;
        });

        //注册到全局，编辑器+运行时都生效
        ConverterGroups.RegisterConverterGroup(group);

        // 创建转换器组，名字后面UI Builder下拉框能看到
        var group3 = new ConverterGroup("BoolToColor2");

        // float → StyleColor
        group3.AddConverter((ref bool source) =>
        {
            StyleColor c;
            if (source == true)
            {
                ColorUtility.TryParseHtmlString("#6E7D91", out Color col);
                c = new StyleColor(col);
            }
            else
            {
                ColorUtility.TryParseHtmlString("#4696FF", out Color col);
                c = new StyleColor(col);
            }
            return c;
        });

        //注册到全局，编辑器+运行时都生效
        ConverterGroups.RegisterConverterGroup(group3);

        var group1 = new ConverterGroup("IntToColor1");
        group1.AddConverter((ref int source) =>
        {
            StyleColor c;
            if (source == 1)
            {
                ColorUtility.TryParseHtmlString("#4696FF", out Color col);
                c = new StyleColor(col);
            }
            else
            {
                ColorUtility.TryParseHtmlString("#6E7D91", out Color col);
                c = new StyleColor(col);
            }
            return c;
        });
        ConverterGroups.RegisterConverterGroup(group1);


        var group2 = new ConverterGroup("IntToColor2");
        group2.AddConverter((ref int source) =>
        {
            StyleColor c;
            if (source == 0)
            {
                ColorUtility.TryParseHtmlString("#4696FF", out Color col);
                c = new StyleColor(col);
            }
            else
            {
                ColorUtility.TryParseHtmlString("#6E7D91", out Color col);
                c = new StyleColor(col);
            }
            return c;
        });
        ConverterGroups.RegisterConverterGroup(group2);

        var group4 = new ConverterGroup("IntToActive1");
        group4.AddConverter((ref int source) =>
        {
            StyleEnum<DisplayStyle> c;
            if (source == 1)
            {
                c = DisplayStyle.Flex; // 显示
            }
            else
            {
                c = DisplayStyle.None; // 隐藏，释放布局
            }
            return c;
        });
        ConverterGroups.RegisterConverterGroup(group4);

        var group5 = new ConverterGroup("IntToActive2");
        group5.AddConverter((ref int source) =>
        {
            StyleEnum<DisplayStyle> c;
            if (source == 2)
            {
                c = DisplayStyle.Flex; // 显示
            }
            else
            {
                c = DisplayStyle.None; // 隐藏，释放布局
            }
            return c;
        });
        ConverterGroups.RegisterConverterGroup(group5);

        var group6 = new ConverterGroup("FloatToTime");
        group5.AddConverter((ref float source) =>
        {
            int hour = Mathf.FloorToInt(岸桥StateData.Get._任务State.用时 / 3600);
            int minute = Mathf.FloorToInt((岸桥StateData.Get._任务State.用时 % 3600) / 60);
            int second = Mathf.FloorToInt(岸桥StateData.Get._任务State.用时 % 60);
            return $"{hour:D2}:{minute:D2}:{second:D2}";
        });
        ConverterGroups.RegisterConverterGroup(group6);
    }
}
