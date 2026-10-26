using System.Runtime.InteropServices;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

// ==========1.定义设备内存状态结构体，必须IInputStateTypeInfo，StructLayout布局 ==========
[StructLayout(LayoutKind.Explicit, Size = 28)]
public struct AnqiaoDeviceState : IInputStateTypeInfo
{
    public FourCC format => new FourCC('A', 'Q', 'D', 'S');
    [FieldOffset(0)]
    [InputControl(name = "大车移动绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 大车移动绑定;
    [FieldOffset(4)]
    [InputControl(name = "小车移动绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 小车移动绑定;
    [FieldOffset(8)]
    [InputControl(name = "吊具升降绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 吊具升降绑定;
    [FieldOffset(12)]
    [InputControl(name = "海左导板上下绑定", layout = "Integer")]
    public int 海左导板上下绑定;
    [FieldOffset(16)]
    [InputControl(name = "海右导板上下绑定", layout = "Integer")]
    public int 海右导板上下绑定;
    [FieldOffset(20)]
    [InputControl(name = "陆左导板上下绑定", layout = "Integer")]
    public int 陆左导板上下绑定;
    [FieldOffset(24)]
    [InputControl(name = "陆右导板上下绑定", layout = "Integer")]
    public int 陆右导板上下绑定;
    [FieldOffset(28)]
    [InputControl(name = "导板全部上下绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 导板全部上下绑定;
    [FieldOffset(32)]
    [InputControl(name = "吊具旋转绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 吊具旋转绑定;
    [FieldOffset(36)]
    [InputControl(name = "吊具前后倾斜绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 吊具前后倾斜绑定;
    [FieldOffset(40)]
    [InputControl(name = "吊具左右倾斜绑定", layout = "Axis", parameters = "clamp,clampMin=-3000,clampMax=3000")]
    public float 吊具左右倾斜绑定;
    [FieldOffset(44)]
    [InputControl(name = "吊具回零绑定", layout = "Button")]
    public bool 吊具回零绑定;
    [FieldOffset(45)]
    [InputControl(name = "吊具泵开绑定", layout = "Button")]
    public bool 吊具泵开绑定;
    [FieldOffset(46)]
    [InputControl(name = "吊具泵关绑定", layout = "Button")]
    public bool 吊具泵关绑定;
    [FieldOffset(47)]
    [InputControl(name = "吊具20英尺绑定", layout = "Button")]
    public bool 吊具20英尺绑定;
    [FieldOffset(48)]
    [InputControl(name = "吊具40英尺绑定", layout = "Button")]
    public bool 吊具40英尺绑定;
    [FieldOffset(49)]
    [InputControl(name = "大车鸣笛绑定", layout = "Button")]
    public bool 大车鸣笛绑定;
    [FieldOffset(50)]
    [InputControl(name = "控制合绑定", layout = "Button")]
    public bool 控制合绑定;
    [FieldOffset(51)]
    [InputControl(name = "控制关绑定", layout = "Button")]
    public bool 控制关绑定;
    [FieldOffset(52)]
    [InputControl(name = "吊具开闭锁绑定", layout = "Integer")]
    public int 吊具开闭锁绑定;
    [FieldOffset(56)]
    [InputControl(name = "夹轮绑定", layout = "Button")]
    public bool 夹轮绑定;
    [FieldOffset(57)]
    [InputControl(name = "松轮绑定", layout = "Button")]
    public bool 松轮绑定;
    [FieldOffset(58)]
    [InputControl(name = "单双箱选择绑定", layout = "Integer")]
    public int 单双箱选择绑定;

}

// ==========2.自定义设备类，指定stateType状态结构体 ==========
[InputControlLayout(displayName = "岸桥", stateType = typeof(AnqiaoDeviceState))]
public class AnqiaoDevice : InputDevice
{
    public AxisControl 大车移动绑定 { get; private set; }
    public AxisControl 小车移动绑定 { get; private set; }
    public AxisControl 吊具升降绑定 { get; private set; }
    public IntegerControl 海左导板上下绑定 { get; private set; }
    public IntegerControl 海右导板上下绑定 { get; private set; }
    public IntegerControl 陆左导板上下绑定 { get; private set; }
    public IntegerControl 陆右导板上下绑定 { get; private set; }
    public AxisControl 导板全部上下绑定 { get; private set; }
    public AxisControl 吊具旋转绑定 { get; private set; }
    public AxisControl 吊具前后倾斜绑定 { get; private set; }
    public AxisControl 吊具左右倾斜绑定 { get; private set; }
    public ButtonControl 吊具回零绑定 { get; private set; }
    public ButtonControl 吊具泵开绑定 { get; private set; }
    public ButtonControl 吊具泵关绑定 { get; private set; }
    public ButtonControl 吊具20英尺绑定 { get; private set; }
    public ButtonControl 吊具40英尺绑定 { get; private set; }
    public ButtonControl 大车鸣笛绑定 { get; private set; }
    public ButtonControl 控制合绑定 { get; private set; }
    public ButtonControl 控制关绑定 { get; private set; }
    public IntegerControl 吊具开闭锁绑定 { get; private set; }
    public ButtonControl 夹轮绑定 { get; private set; }
    public ButtonControl 松轮绑定 { get; private set; }
    public IntegerControl 单双箱选择绑定 { get; private set; }
    protected override void FinishSetup()
    {
        base.FinishSetup();
        大车移动绑定 = GetChildControl<AxisControl>("大车移动绑定");
        小车移动绑定 = GetChildControl<AxisControl>("小车移动绑定");
        吊具升降绑定 = GetChildControl<AxisControl>("吊具升降绑定");
        海左导板上下绑定 = GetChildControl<IntegerControl>("海左导板上下绑定");
        海右导板上下绑定 = GetChildControl<IntegerControl>("海右导板上下绑定");
        陆左导板上下绑定 = GetChildControl<IntegerControl>("陆左导板上下绑定");
        陆右导板上下绑定 = GetChildControl<IntegerControl>("陆右导板上下绑定");
        导板全部上下绑定 = GetChildControl<AxisControl>("导板全部上下绑定");
        吊具旋转绑定 = GetChildControl<AxisControl>("吊具旋转绑定");
        吊具前后倾斜绑定 = GetChildControl<AxisControl>("吊具前后倾斜绑定");
        吊具左右倾斜绑定 = GetChildControl<AxisControl>("吊具左右倾斜绑定");
        吊具回零绑定 = GetChildControl<ButtonControl>("吊具回零绑定");
        吊具泵开绑定 = GetChildControl<ButtonControl>("吊具泵开绑定");
        吊具泵关绑定 = GetChildControl<ButtonControl>("吊具泵关绑定");
        吊具20英尺绑定 = GetChildControl<ButtonControl>("吊具20英尺绑定");
        吊具40英尺绑定 = GetChildControl<ButtonControl>("吊具40英尺绑定");
        大车鸣笛绑定 = GetChildControl<ButtonControl>("大车鸣笛绑定");
        控制合绑定 = GetChildControl<ButtonControl>("控制合绑定");
        控制关绑定 = GetChildControl<ButtonControl>("控制关绑定");
        吊具开闭锁绑定 = GetChildControl<IntegerControl>("吊具开闭锁绑定");
        夹轮绑定 = GetChildControl<ButtonControl>("夹轮绑定");
        松轮绑定 = GetChildControl<ButtonControl>("松轮绑定");
        单双箱选择绑定 = GetChildControl<IntegerControl>("单双箱选择绑定");
    }

    /// <summary>
    /// ✅新版正确上报状态，主线程调用！替代旧的GetStateEventPtr
    /// </summary>
    public void ReportState(AnqiaoDeviceState state)
    {
        InputSystem.QueueStateEvent(this, state);
        InputSystem.Update();

    }
    public static AnqiaoDevice Create()
    {
        return InputSystem.AddDevice<AnqiaoDevice>();
    }

    public void DestroyDevice()
    {
        InputSystem.RemoveDevice(this);
    }
}
