# UI Toolkit DatePicker

Vendored single-date runtime control from [One Mechanic Games](https://github.com/onemechanicgames/Unity-UIToolkit-DatePicker), pinned to commit `03cfd1e4e6fd5d011164ed992e88cb971635a2c2`.

Upstream files: `DatePicker.cs`, `GridDayInfo.cs`, and `MonthLabelFormat.cs` under `Assets/One Mechanic Games/Date Picker (UI Toolkit)/Date Picker/`. The complete MIT license is retained in `LICENSE.txt`. No sample scene, upstream stylesheet, package dependency, or separate assembly definition is imported.

Local compatibility changes:

- Replace generated `UxmlElement`/`UxmlAttribute` registration with `UxmlFactory`/`UxmlTraits` for Unity 6000.0 and source-only compilation. Traits retain the year range, month format, deselection, current-month visibility, and dropdown-height attributes. Preserve the control and factory for player builds that strip unused types.
- Remove the finalizer that touched UI objects during garbage collection.
- Use Chinese weekday/month labels and text navigation arrows instead of external PNG sprites.
- Ignore empty or out-of-range dropdown selections while month/year lists are rebuilt.
- Keep dropdowns open when pointer events bubble from their scrollbars; close only on background clicks or completed selections, and reset dropdown state when the host resets the date.
- Add `SetMarkedDates(IEnumerable<DateTime>)` and a non-picking dot per date cell so the consuming teacher UI can mark dates containing score records. Markers persist across month changes and do not change selection or emit date events.

The upstream calendar grid and date-selection algorithms are unchanged. `show-current-month-dates-only` remains false by default; its upstream visibility behavior is not used by TeacherHome.

UXML registration: `xmlns:calendar="OMG.UI.DatePickerUITK"`, then `<calendar:DatePicker name="calendar" min-year="1990" max-year="2100" click-again-to-deselect="false" />`. The control uses the containing `YardCraneSimulator.Shell` assembly and requires a stylesheet supplied by its consuming UI.

Runtime API: `CurrentDate`, `value`, `SetValueWithoutNotify(DateTime?)`, and `RegisterValueChangedCallback` with `ChangeEvent<DateTime?>`.
