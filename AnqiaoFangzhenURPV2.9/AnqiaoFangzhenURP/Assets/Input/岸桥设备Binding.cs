using game4automation;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.Rendering.DebugUI;
public class 岸桥设备Binding : MonoBehaviour
{
    private AnqiaoDeviceState _AnqiaoDeviceState;
    private AnqiaoDevice _AnqiaoDevice;
    public 岸桥Control _岸桥Control;
    public 岸桥StateCreat _岸桥State;
    private void Awake()
    {
        _岸桥State.ResetDefault();
        _AnqiaoDevice = AnqiaoDevice.Create();
        _AnqiaoDeviceState = new AnqiaoDeviceState();
    }
    private void Update()
    {
        _AnqiaoDevice.ReportState(_AnqiaoDeviceState);
    }

    private void OnDestroy()
    {
        _AnqiaoDevice.DestroyDevice();
    }
    #region Binding
    //大车移动
    public void Binding_大车移动(float value)
    {
        _AnqiaoDeviceState.大车移动绑定 = value;
    }
    public void Binding_大车移动(Signal signal)
    {
        _AnqiaoDeviceState.大车移动绑定 = (float)signal.GetValue();
    }
    //小车移动
    public void Binding_小车移动(float value)
    {
        _AnqiaoDeviceState.小车移动绑定 = value;
    }
    public void Binding_小车移动(Signal signal)
    {
        _AnqiaoDeviceState.小车移动绑定 = (float)signal.GetValue();
    }
    //吊具升降
    public void Binding_吊具升降(float value)
    {
        _AnqiaoDeviceState.吊具升降绑定 = value;
    }
    public void Binding_吊具升降(Signal signal)
    {
        _AnqiaoDeviceState.吊具升降绑定 = (float)signal.GetValue();
    }
    //海左导板上下
    public void Binding_海左导板上下(int value)
    {
        if (岸桥StateData.Get._岸桥State.海左导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.海左导板上下绑定 = value;
    }
    public void Binding_海左导板上下(Signal signal)
    {
        if (岸桥StateData.Get._岸桥State.海左导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.海左导板上下绑定 = (int)signal.GetValue();
    }
    //海右导板上下
    public void Binding_海右导板上下(int value)
    {
        if (岸桥StateData.Get._岸桥State.海右导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.海右导板上下绑定 = value;
    }
    public void Binding_海右导板上下(Signal signal)
    {
        if (岸桥StateData.Get._岸桥State.海右导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.海右导板上下绑定 = (int)signal.GetValue();
    }
    //陆左导板上下
    public void Binding_陆左导板上下(int value)
    {
        if (岸桥StateData.Get._岸桥State.陆左导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.陆左导板上下绑定 = value;
    }
    public void Binding_陆左导板上下(Signal signal)
    {
        if (岸桥StateData.Get._岸桥State.陆左导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.陆左导板上下绑定 = (int)signal.GetValue();
    }
    //陆右导板上下
    public void Binding_陆右导板上下(int value)
    {
        if (岸桥StateData.Get._岸桥State.陆右导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.陆右导板上下绑定 = value;
    }
    public void Binding_陆右导板上下(Signal signal)
    {
        if (岸桥StateData.Get._岸桥State.陆右导板上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.海右导板上下绑定 = (int)signal.GetValue();
    }
    //导板全部上下
    public void Binding_导板全部上下(float value)
    {
        if (岸桥StateData.Get._岸桥State.导板全部上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.导板全部上下绑定 = value;
    }
    public void Binding_导板全部上下(Signal signal)
    {
        if (岸桥StateData.Get._岸桥State.导板全部上下 == 2)
        {
            return;
        }
        _AnqiaoDeviceState.导板全部上下绑定 = (float)signal.GetValue();
    }
    //吊具旋转
    public void Binding_吊具旋转(float value)
    {
        _AnqiaoDeviceState.吊具旋转绑定 = value;
    }
    public void Binding_吊具旋转(Signal signal)
    {
        _AnqiaoDeviceState.吊具旋转绑定 = (float)signal.GetValue();
    }
    //吊具前后倾斜
    public void Binding_吊具前后倾斜(float value)
    {
        _AnqiaoDeviceState.吊具前后倾斜绑定 = value;
    }
    public void Binding_吊具前后倾斜(Signal signal)
    {
        _AnqiaoDeviceState.吊具前后倾斜绑定 = (float)signal.GetValue();
    }
    //吊具左右倾斜
    public void Binding_吊具左右倾斜(float value)
    {
        _AnqiaoDeviceState.吊具左右倾斜绑定 = value;
    }
    public void Binding_吊具左右倾斜(Signal signal)
    {
        _AnqiaoDeviceState.吊具左右倾斜绑定 = (float)signal.GetValue();
    }
    //吊具回零
    public void Binding_吊具回零()
    {
        _AnqiaoDeviceState.吊具回零绑定 = true;
    }
    public void Binding_吊具回零(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.吊具回零绑定 = true;
        }
    }
    //吊具泵开
    public void Binding_吊具泵开()
    {
        _AnqiaoDeviceState.吊具泵开绑定 = true;
    }
    public void Binding_吊具泵开(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.吊具泵开绑定 = true;
        }
    }
    //吊具泵关
    public void Binding_吊具泵关()
    {
        _AnqiaoDeviceState.吊具泵关绑定 = true;
    }
    public void Binding_吊具泵关(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.吊具泵关绑定 = true;
        }
    }
    //吊具20英尺
    public void Binding_吊具20英尺()
    {
        _AnqiaoDeviceState.吊具20英尺绑定 = true;
    }
    public void Binding_吊具20英尺(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.吊具20英尺绑定 = true;
        }
    }
    //吊具40英尺
    public void Binding_吊具40英尺()
    {
        _AnqiaoDeviceState.吊具40英尺绑定 = true;
    }
    public void Binding_吊具40英尺(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.吊具40英尺绑定 = true;
        }
    }
    //大车鸣笛
    public void Binding_大车鸣笛()
    {
        _AnqiaoDeviceState.大车鸣笛绑定 = true;
    }
    public void Binding_大车鸣笛(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.大车鸣笛绑定 = true;
        }
    }
    //控制合
    public void Binding_控制合()
    {
        _AnqiaoDeviceState.控制合绑定 = true;
    }
    public void Binding_控制合(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.控制合绑定 = true;
        }
    }
    //控制关
    public void Binding_控制关()
    {
        _AnqiaoDeviceState.控制关绑定 = true;
    }
    public void Binding_控制关(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.控制关绑定 = true;
        }
    }
    //吊具开闭锁
    public void Binding_吊具开闭锁(int value)
    {
        if (value == 0)//通知开锁
        {
            if (岸桥StateData.Get._岸桥State.集装箱状态 == 2)//只有着箱闭锁状态下才能开锁
            {
                _AnqiaoDeviceState.吊具开闭锁绑定 = value;
            }
        }
        else//通知闭锁
        {
            if (岸桥StateData.Get._岸桥State.着箱 == true)//只有着箱才能闭锁
            {
                _AnqiaoDeviceState.吊具开闭锁绑定 = value;
            }

        }
    }
    public void Binding_吊具开闭锁(Signal signal)
    {
        var value = (int)signal.GetValue();
        if (value == 0)//通知开锁
        {
            if (岸桥StateData.Get._岸桥State.集装箱状态 == 2)//只有着箱闭锁状态下才能开锁
            {
                _AnqiaoDeviceState.吊具开闭锁绑定 = value;
            }
        }
        else//通知闭锁
        {
            if (岸桥StateData.Get._岸桥State.着箱 == true)//只有着箱才能闭锁
            {
                _AnqiaoDeviceState.吊具开闭锁绑定 = value;
            }

        }
    }
    //夹轮
    public void Binding_夹轮()
    {
        _AnqiaoDeviceState.夹轮绑定 = true;
    }
    public void Binding_夹轮(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.夹轮绑定 = true;
        }
    }
    //松轮
    public void Binding_松轮()
    {
        _AnqiaoDeviceState.松轮绑定 = true;
    }
    public void Binding_松轮(Signal signal)
    {
        if (bool.Parse(signal.GetValue().ToString()) == true)
        {
            _AnqiaoDeviceState.松轮绑定 = true;
        }
    }
    //单双箱选择
    public void Binding_单双箱选择(int value)
    {
        if (岸桥StateData.Get._岸桥State.吊具尺寸 == 1)//40状态可切换
        {
            _AnqiaoDeviceState.单双箱选择绑定 = value;
        }
        else
        {

        }
    }
    public void Binding_单双箱选择(Signal signal)
    {
        var value = (int)signal.GetValue();
        if (岸桥StateData.Get._岸桥State.吊具尺寸 == 1)//40状态可切换
        {
            _AnqiaoDeviceState.单双箱选择绑定 = value;
        }
        else
        {

        }
    }
    #endregion
    #region Action
    private float _last大车移动 = -10;
    public void Action_大车移动(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last大车移动) return;
        _last大车移动 = v;
        //数据转换处理

        if (_岸桥State.控制合关 == true)
        {
            if (_岸桥State.夹松轮 == false)
            {
                _岸桥State.大车速度 = v;
                print("大车移动" + v);
                _岸桥Control.大车移动Control?.Invoke(v);
            }
            else
            {
                _岸桥State.大车速度 = 0;
                print("大车移动" + 0);
                _岸桥Control.大车移动Control?.Invoke(0);
            }
        }
        else
        {
            _岸桥State.大车速度 = 0;
            print("大车移动" + 0);
            _岸桥Control.大车移动Control?.Invoke(0);
        }
    }
    private float _last小车移动 = -10;
    public void Action_小车移动(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last小车移动) return;
        _last小车移动 = v;
        //数据转换处理
        if (_岸桥State.控制合关 == true)
        {
            _岸桥State.小车速度 = v;
            print("小车移动" + v);
            _岸桥Control.小车移动Control?.Invoke(v);
        }
        else
        {
            _岸桥State.小车速度 = 0;
            print("小车移动" + 0);
            _岸桥Control.小车移动Control?.Invoke(0);
        }
    }
    private float _last吊具升降 = -10;
    public void Action_吊具升降(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last吊具升降) return;
        _last吊具升降 = v;
        //数据转换处理
        if (_岸桥State.控制合关 == true)
        {
            _岸桥State.吊具速度 = v;
            print("吊具升降" + v);
            _岸桥Control.吊具升降Control?.Invoke(v);
        }
        else
        {
            _岸桥State.吊具速度 = 0;
            print("吊具升降" + 0);
            _岸桥Control.吊具升降Control?.Invoke(0);
        }
    }
    private int _last海左导板上下 = -10;
    public void Action_海左导板上下(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last海左导板上下) return;
        _last海左导板上下 = v;
        //数据转换处理

        _岸桥State.海左导板上下 = v;
        print("海左导板上下" + v);
        _岸桥Control.海左导板上下Control?.Invoke(v);
    }
    private int _last海右导板上下 = -10;
    public void Action_海右导板上下(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last海右导板上下) return;
        _last海右导板上下 = v;
        //数据转换处理
        _岸桥State.海右导板上下 = v;
        print("海右导板上下" + v);
        _岸桥Control.海右导板上下Control?.Invoke(v);
    }
    private int _last陆左导板上下 = -10;
    public void Action_陆左导板上下(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last陆左导板上下) return;
        _last陆左导板上下 = v;
        //数据转换处理
        _岸桥State.陆左导板上下 = v;
        print("陆左导板上下" + v);
        _岸桥Control.陆左导板上下Control?.Invoke(v);
    }
    private int _last陆右导板上下 = -10;
    public void Action_陆右导板上下(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last陆右导板上下) return;
        _last陆右导板上下 = v;
        //数据转换处理
        _岸桥State.陆右导板上下 = v;
        print("陆右导板上下" + v);
        _岸桥Control.陆右导板上下Control?.Invoke(v);
    }
    private float _last导板全部上下 = -10;
    public void Action_导板全部上下(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last导板全部上下) return;
        _last导板全部上下 = v;
        //数据转换处理
        _岸桥State.导板全部上下 = v;
        print("导板全部上下" + v);
        _岸桥Control.导板全部上下Control?.Invoke(v);
    }
    private float _last吊具旋转 = -10;
    public void Action_吊具旋转(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last吊具旋转) return;
        _last吊具旋转 = v;
        //数据转换处理
        if (_岸桥State.吊具回零状态 == false)
        {
            _岸桥State.吊具旋转速度 = v;
            print("吊具旋转" + v);
            _岸桥Control.吊具旋转Control?.Invoke(v);
        }
    }
    private float _last前后倾斜 = -10;
    public void Action_吊具前后倾斜(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last前后倾斜) return;
        _last前后倾斜 = v;
        //数据转换处理
        if (_岸桥State.吊具回零状态 == false)
        {
            _岸桥State.吊具前后倾斜速度 = v;
            print("吊具前后倾斜" + v);
            _岸桥Control.吊具前后倾斜Control?.Invoke(v);
        }
    }
    private float _last左右倾斜 = -10;
    public void Action_吊具左右倾斜(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<float>();
        if (v == _last左右倾斜) return;
        _last左右倾斜 = v;
        //数据转换处理
        if (_岸桥State.吊具回零状态 == false)
        {
            _岸桥State.吊具左右倾斜速度 = v;
            print("吊具左右倾斜" + v);
            _岸桥Control.吊具左右倾斜Control?.Invoke(v);
        }
    }
    public void Action_吊具回零(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.吊具回零 = true;
            print("吊具回零");
            if (_岸桥State.吊具回零状态 == false)
            {
                _岸桥Control.吊具回零Control?.Invoke();
            }
            _AnqiaoDeviceState.吊具回零绑定 = false;
        }
    }
    public void Action_吊具泵开(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.吊具泵开关 = true;
            print("吊具泵开");
            _岸桥Control.吊具泵开Control?.Invoke();
            _AnqiaoDeviceState.吊具泵开绑定 = false;
        }
    }
    public void Action_吊具泵关(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.吊具泵开关 = false;
            print("吊具泵关");
            _岸桥Control.吊具泵关Control?.Invoke();
            _AnqiaoDeviceState.吊具泵关绑定 = false;
        }
    }
    public void Action_吊具20英尺(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            //_岸桥State.吊具尺寸 = 0;
            print("吊具20英尺");
            _岸桥Control.吊具20英尺Control?.Invoke();
            _AnqiaoDeviceState.吊具20英尺绑定 = false;
        }
    }
    public void Action_吊具40英尺(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            //_岸桥State.吊具尺寸 = 1;
            print("吊具40英尺");
            _岸桥Control.吊具40英尺Control?.Invoke();
            _AnqiaoDeviceState.吊具40英尺绑定 = false;
        }
    }
    public void Action_大车鸣笛(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.大车鸣笛 = true;
            print("大车鸣笛");
            _岸桥Control.大车鸣笛Control?.Invoke();
            _AnqiaoDeviceState.大车鸣笛绑定 = false;
        }
    }
    public void Action_控制合(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.控制合关 = true;
            print("控制合");
            _岸桥Control.控制合Control?.Invoke();
            _AnqiaoDeviceState.控制合绑定 = false;
        }
    }
    public void Action_控制关(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.控制合关 = false;
            print("控制关");
            _岸桥Control.控制关Control?.Invoke();
            _AnqiaoDeviceState.控制关绑定 = false;
        }
    }
    private int _last吊具开闭锁 = -10;
    public void Action_吊具开闭锁(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last吊具开闭锁) return;
        _last吊具开闭锁 = v;
        //数据转换处理
        _岸桥State.吊具开闭锁 = v;
        print("吊具开闭锁" + v);
        _岸桥Control.吊具开闭锁Control?.Invoke(v);
    }
    public void Action_夹轮(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.夹松轮 = true;
            print("夹轮");
            _岸桥Control.夹轮Control?.Invoke();
            _AnqiaoDeviceState.夹轮绑定 = false;
        }
    }
    public void Action_松轮(InputAction.CallbackContext ctx)
    {
        if (ctx.phase == InputActionPhase.Started)
        {
            _岸桥State.夹松轮 = false;
            print("松轮");
            _岸桥Control.松轮Control?.Invoke();
            _AnqiaoDeviceState.松轮绑定 = false;
        }
    }
    private int _last单双箱选择 = -10;
    public void Action_单双箱选择(InputAction.CallbackContext ctx)
    {
        var v = ctx.ReadValue<int>();
        if (v == _last单双箱选择) return;
        _last单双箱选择 = v;
        //数据转换处理
        _岸桥State.单双箱选择 = v;
        print("单双箱选择" + v);
        _岸桥Control.单双箱选择Control?.Invoke(v);
    }
    #endregion
}
