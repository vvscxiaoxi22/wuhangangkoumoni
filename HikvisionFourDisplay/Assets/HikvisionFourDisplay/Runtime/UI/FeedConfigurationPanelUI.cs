using System;
using System.Collections.Generic;
using HikvisionFourDisplay.Runtime.Bootstrap;
using HikvisionFourDisplay.Runtime.Configuration;
using HikvisionFourDisplay.Runtime.Security;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.UI
{
    /// <summary>
    /// Operator-facing runtime configuration panel for the four camera feeds. Built with
    /// IMGUI (OnGUI) rather than UGUI/UI Toolkit: it needs no package beyond core Unity
    /// modules already in this project, so it cannot reintroduce the local Package Manager
    /// initialization issue the batch validation already works around with -noUpm (see
    /// README.md). IMGUI also only renders to the primary display (Unity Display index 0) in
    /// a multi-display Standalone build, which conveniently keeps this control surface off
    /// the three pure video-wall screens without any extra targeting logic.
    ///
    /// On-site operators only ever have the built Standalone Player, not the Editor, so this
    /// is the field-deployable counterpart to the Editor-only
    /// Tools/Hikvision Four Display/Manage Feed Credentials window.
    /// </summary>
    public sealed class FeedConfigurationPanelUI : MonoBehaviour
    {
        [Serializable]
        private sealed class CameraFormState
        {
            public bool enabled;
            public string host = string.Empty;
            public string rtspPort = "554";
            public string channel = "101";
            public bool isSubStream;
            public string credentialKey = string.Empty;
            public string username = string.Empty;
            public string password = string.Empty;
            public int rotationDegrees;
            public bool cameraPickerOpen;
            public Vector2 cameraPickerScroll;
        }

        private const int CameraCount = 4;
        private const float PanelWidth = 520f;
        private const float StartButtonWidth = 156f;
        private const float StartButtonHeight = 42f;
        private const float Margin = 24f;
        private const int TextureSize = 64;
        private const int TextureCorner = 16;

        private FourDisplayBootstrap bootstrap;
        private readonly CameraFormState[] forms = new CameraFormState[CameraCount];
        private bool panelOpen;
        private int selectedCanvasDisplayIndex = -1;
        private string statusMessage = string.Empty;
        private string configFilePath = string.Empty;
        private Vector2 scrollPosition;
        private FeedSchemeDocument schemes = new FeedSchemeDocument();
        private CameraCatalogDocument cameraCatalog = new CameraCatalogDocument();
        private string schemeName = string.Empty;
        private string activeSchemeId = string.Empty;
        private bool choicesLoaded;

        private GUIStyle panelStyle;
        private GUIStyle panelTitleStyle;
        private GUIStyle panelSubtitleStyle;
        private GUIStyle overlineStyle;
        private GUIStyle sectionTitleStyle;
        private GUIStyle sectionCodeStyle;
        private GUIStyle sectionStyle;
        private GUIStyle pathStyle;
        private GUIStyle fieldLabelStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle secondaryButtonStyle;
        private GUIStyle closeButtonStyle;
        private GUIStyle startButtonStyle;
        private GUIStyle enabledPillStyle;
        private GUIStyle disabledPillStyle;
        private GUIStyle selectedSegmentStyle;
        private GUIStyle unselectedSegmentStyle;
        private GUIStyle statusStyle;
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private bool stylesReady;

        public void Configure(FourDisplayBootstrap fourDisplayBootstrap)
        {
            bootstrap = fourDisplayBootstrap;
        }

        private void Awake()
        {
            for (var i = 0; i < CameraCount; i++) forms[i] = new CameraFormState();
            ResolveBootstrapReference();
        }

        private void OnDestroy()
        {
            foreach (var texture in generatedTextures)
            {
                if (texture != null) Destroy(texture);
            }
            generatedTextures.Clear();
        }

        private bool ResolveBootstrapReference()
        {
            if (bootstrap != null) return true;

            bootstrap = UnityEngine.Object.FindFirstObjectByType<FourDisplayBootstrap>();
            if (bootstrap != null)
            {
                Debug.Log("[HFD_CONFIG_UI_BOOTSTRAP] status=resolved_at_runtime");
                return true;
            }

            Debug.LogError("[HFD_CONFIG_UI_BOOTSTRAP] status=missing");
            return false;
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (!panelOpen)
            {
                DrawStartButton();
                return;
            }

            DrawPanel();
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            var panelBackground = CreateRoundedTexture(
                new Color32(4, 29, 68, 246), new Color32(112, 218, 248, 176), 18f, 1.5f);
            var cardBackground = CreateRoundedTexture(
                new Color32(8, 43, 87, 238), new Color32(112, 218, 248, 116), 15f, 1.25f);
            var fieldBackground = CreateRoundedTexture(
                new Color32(3, 25, 55, 232), new Color32(113, 193, 229, 94), 10f, 1f);
            var fieldFocusBackground = CreateRoundedTexture(
                new Color32(8, 44, 82, 246), new Color32(89, 224, 255, 224), 10f, 1.5f);
            var primaryBackground = CreateRoundedTexture(
                new Color32(42, 148, 241, 244), new Color32(164, 239, 255, 224), 12f, 1.25f);
            var primaryHoverBackground = CreateRoundedTexture(
                new Color32(46, 169, 255, 255), new Color32(211, 248, 255, 242), 12f, 1.5f);
            var primaryActiveBackground = CreateRoundedTexture(
                new Color32(29, 119, 213, 255), new Color32(132, 239, 255, 226), 12f, 1.5f);
            var secondaryBackground = CreateRoundedTexture(
                new Color32(15, 59, 105, 224), new Color32(154, 224, 249, 108), 12f, 1f);
            var secondaryHoverBackground = CreateRoundedTexture(
                new Color32(35, 112, 173, 244), new Color32(151, 239, 255, 224), 12f, 1.25f);
            var activePillBackground = CreateRoundedTexture(
                new Color32(33, 127, 166, 214), new Color32(111, 230, 245, 210), 14f, 1f);
            var inactivePillBackground = CreateRoundedTexture(
                new Color32(17, 51, 88, 214), new Color32(137, 196, 226, 88), 14f, 1f);
            var statusBackground = CreateRoundedTexture(
                new Color32(10, 52, 94, 232), new Color32(105, 214, 244, 92), 10f, 1f);
            var pathBackground = CreateRoundedTexture(
                new Color32(2, 21, 48, 176), new Color32(149, 215, 243, 44), 9f, 1f);
            var transparentBackground = CreateRoundedTexture(
                new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), 8f, 0f);
            var scrollThumbBackground = CreateRoundedTexture(
                new Color32(78, 224, 255, 142), new Color32(176, 232, 255, 166), 8f, 1f);

            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(18, 18, 18, 18),
                border = new RectOffset(TextureCorner, TextureCorner, TextureCorner, TextureCorner)
            };
            panelStyle.normal.background = panelBackground;

            panelTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color32(245, 251, 255, 255) }
            };
            panelSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                normal = { textColor = new Color32(191, 222, 239, 184) }
            };
            overlineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color32(111, 230, 245, 255) }
            };
            sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color32(244, 250, 255, 255) }
            };
            sectionCodeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color32(78, 224, 255, 226) }
            };
            sectionStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 13, 14),
                margin = new RectOffset(0, 8, 5, 8),
                border = new RectOffset(TextureCorner, TextureCorner, TextureCorner, TextureCorner)
            };
            sectionStyle.normal.background = cardBackground;
            pathStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8),
                border = new RectOffset(10, 10, 10, 10),
                normal =
                {
                    background = pathBackground,
                    textColor = new Color32(171, 210, 231, 184)
                }
            };
            fieldLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color32(177, 211, 234, 214) }
            };
            textFieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 14,
                fixedHeight = 38f,
                padding = new RectOffset(12, 12, 7, 7),
                border = new RectOffset(11, 11, 11, 11),
                normal = { background = fieldBackground, textColor = new Color32(239, 249, 255, 255) },
                hover = { background = fieldFocusBackground, textColor = Color.white },
                focused = { background = fieldFocusBackground, textColor = Color.white }
            };

            primaryButtonStyle = CreateButtonStyle(
                primaryBackground, primaryHoverBackground, primaryActiveBackground,
                new Color32(249, 253, 255, 255), 14, 42f, FontStyle.Bold);
            secondaryButtonStyle = CreateButtonStyle(
                secondaryBackground, secondaryHoverBackground, primaryActiveBackground,
                new Color32(226, 243, 255, 255), 13, 40f, FontStyle.Normal);
            startButtonStyle = CreateButtonStyle(
                secondaryBackground, secondaryHoverBackground, primaryActiveBackground,
                new Color32(245, 252, 255, 255), 14, StartButtonHeight, FontStyle.Bold);
            closeButtonStyle = CreateButtonStyle(
                transparentBackground, secondaryHoverBackground, primaryActiveBackground,
                new Color32(226, 243, 255, 235), 24, 38f, FontStyle.Normal);
            enabledPillStyle = CreateButtonStyle(
                activePillBackground, primaryHoverBackground, primaryActiveBackground,
                new Color32(181, 245, 255, 255), 11, 30f, FontStyle.Bold);
            disabledPillStyle = CreateButtonStyle(
                inactivePillBackground, secondaryHoverBackground, primaryActiveBackground,
                new Color32(163, 199, 224, 224), 11, 30f, FontStyle.Normal);
            selectedSegmentStyle = CreateButtonStyle(
                activePillBackground, primaryHoverBackground, primaryActiveBackground,
                new Color32(244, 250, 255, 255), 12, 34f, FontStyle.Bold);
            unselectedSegmentStyle = CreateButtonStyle(
                inactivePillBackground, secondaryHoverBackground, primaryActiveBackground,
                new Color32(177, 211, 234, 224), 12, 34f, FontStyle.Normal);
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(12, 12, 10, 10),
                border = new RectOffset(11, 11, 11, 11),
                normal =
                {
                    background = statusBackground,
                    textColor = new Color32(213, 242, 252, 255)
                }
            };

            GUI.skin.verticalScrollbar.fixedWidth = 8f;
            GUI.skin.verticalScrollbar.normal.background = transparentBackground;
            GUI.skin.verticalScrollbarThumb.fixedWidth = 8f;
            GUI.skin.verticalScrollbarThumb.normal.background = scrollThumbBackground;
            GUI.skin.verticalScrollbarThumb.hover.background = primaryHoverBackground;
            GUI.skin.verticalScrollbarThumb.active.background = primaryActiveBackground;
        }

        private void DrawStartButton()
        {
            var rect = new Rect(Screen.width - StartButtonWidth - Margin, Margin, StartButtonWidth, StartButtonHeight);
            if (GUI.Button(rect, "视频源配置", startButtonStyle))
            {
                LoadFormsFromCurrentConfig();
                choicesLoaded = false;
                EnsureChoicesLoaded();
                panelOpen = true;
            }
        }

        private void DrawPanel()
        {
            var panelWidth = Mathf.Min(PanelWidth, Screen.width - Margin * 2f);
            var panelHeight = Mathf.Clamp(Screen.height - Margin * 2f, 420f, 820f);
            var rect = new Rect(Screen.width - panelWidth - Margin, Margin, panelWidth, panelHeight);

            GUILayout.BeginArea(rect, panelStyle);
            DrawTopHighlight(panelWidth);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("CAMERA MATRIX  /  LIVE VIDEO", overlineStyle);
            GUILayout.Label("四路摄像机配置", panelTitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", closeButtonStyle, GUILayout.Width(42f))) panelOpen = false;
            GUILayout.EndHorizontal();

            GUILayout.Label("每路独立保存与重连，其余画面保持连续播放", panelSubtitleStyle);
            GUILayout.Space(8);
            DrawDivider(new Color32(105, 214, 244, 64));
            GUILayout.Space(8);
            GUILayout.Label($"CONFIG  ·  {configFilePath}", pathStyle);
            GUILayout.Space(8);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            DrawSchemeSection();
            for (var i = 0; i < CameraCount; i++) DrawCameraSection(i);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            if (!string.IsNullOrEmpty(statusMessage)) GUILayout.Label(statusMessage, statusStyle);

            GUILayout.Space(4);
            if (GUILayout.Button("关闭配置", secondaryButtonStyle)) panelOpen = false;

            GUILayout.EndArea();
        }

        private void DrawCameraSection(int index)
        {
            var displayNumber = index + 1;
            var form = forms[index];

            GUILayout.BeginVertical(sectionStyle);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label($"CAMERA {displayNumber:00}", sectionCodeStyle);
            GUILayout.Label($"摄像头 {displayNumber}  ·  DISPLAY {displayNumber}", sectionTitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    form.enabled ? "●  已启用" : "○  已停用",
                    form.enabled ? enabledPillStyle : disabledPillStyle,
                    GUILayout.Width(92f)))
            {
                form.enabled = !form.enabled;
            }
            GUILayout.EndHorizontal();

            var canvasSelected = selectedCanvasDisplayIndex == index;
            var canvasSelection = GUILayout.Toggle(
                canvasSelected,
                canvasSelected ? "✓  Canvas 显示在此屏" : "Canvas 显示在此屏",
                canvasSelected ? enabledPillStyle : disabledPillStyle);
            if (canvasSelection != canvasSelected)
                selectedCanvasDisplayIndex = canvasSelection ? index : -1;

            GUILayout.Space(8);
            DrawCameraPicker(form);
            GUILayout.Label("摄像机地址  /  IP ADDRESS", fieldLabelStyle);
            form.host = GUILayout.TextField(form.host, textFieldStyle);

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(132f));
            GUILayout.Label("RTSP 端口", fieldLabelStyle);
            form.rtspPort = GUILayout.TextField(form.rtspPort, textFieldStyle);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.BeginVertical(GUILayout.Width(110f));
            GUILayout.Label("通道", fieldLabelStyle);
            form.channel = GUILayout.TextField(form.channel, textFieldStyle);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.BeginVertical();
            GUILayout.Label("码流类型", fieldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("主码流", form.isSubStream ? unselectedSegmentStyle : selectedSegmentStyle))
                form.isSubStream = false;
            if (GUILayout.Button("子码流", form.isSubStream ? selectedSegmentStyle : unselectedSegmentStyle))
                form.isSubStream = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("用户名", fieldLabelStyle);
            form.username = GUILayout.TextField(form.username, textFieldStyle);
            GUILayout.EndVertical();
            GUILayout.Space(8);
            GUILayout.BeginVertical();
            GUILayout.Label("密码（留空保留原密码）", fieldLabelStyle);
            form.password = GUILayout.PasswordField(form.password, '*', textFieldStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label($"画面方向  /  当前顺时针 {form.rotationDegrees}°", fieldLabelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("左转 90°", secondaryButtonStyle)) RotateFeed(index, -90);
            if (GUILayout.Button("右转 90°", secondaryButtonStyle)) RotateFeed(index, 90);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            if (GUILayout.Button($"保存并连接  ·  CAMERA {displayNumber:00}", primaryButtonStyle))
                SaveAndReconnectFeed(index);

            GUILayout.EndVertical();
        }

        private void EnsureChoicesLoaded()
        {
            if (choicesLoaded) return;
            choicesLoaded = true;
            try
            {
                cameraCatalog = CameraCatalogLoader.Load(out var warnings, out _);
                foreach (var warning in warnings) Debug.LogWarning($"[HFD_CAMERA_CATALOG_WARNING] {warning}");
            }
            catch (Exception ex)
            {
                statusMessage = $"读取摄像头列表失败：{ex.GetType().Name}，原文件已保留。";
                Debug.LogError($"[HFD_CAMERA_CATALOG_LOAD_ERROR] exceptionType={ex.GetType().Name}");
            }
            try
            {
                schemes = FeedSchemeStore.Load(out var schemeWarnings);
                foreach (var warning in schemeWarnings) Debug.LogWarning($"[HFD_SCHEME_WARNING] {warning}");
                activeSchemeId = FeedConfigurationLoader.Load(out _, out _).activeSchemeId ?? string.Empty;
            }
            catch (Exception ex)
            {
                statusMessage = $"读取方案列表失败：{ex.GetType().Name}，原文件已保留。";
                Debug.LogError($"[HFD_SCHEME_LOAD_ERROR] exceptionType={ex.GetType().Name}");
            }
        }

        private void DrawSchemeSection()
        {
            EnsureChoicesLoaded();
            GUILayout.BeginVertical(sectionStyle);
            GUILayout.Label("四屏配置方案", sectionTitleStyle);
            GUILayout.Label("保存当前四路配置，点击方案按钮切换展示", panelSubtitleStyle);
            GUILayout.Space(5);
            GUILayout.Label("方案名称（同名保存将更新原方案）", fieldLabelStyle);
            schemeName = GUILayout.TextField(schemeName, textFieldStyle);
            if (GUILayout.Button("保存四屏方案", primaryButtonStyle)) SaveScheme();
            DrawSchemeButtons();
            GUILayout.EndVertical();
        }

        private void DrawSchemeButtons()
        {
            var visibleSchemes = schemes.schemes;
            if (visibleSchemes == null) return;
            string schemeToDelete = null;
            foreach (var scheme in visibleSchemes)
            {
                if (scheme == null) continue;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(scheme.name,
                        string.Equals(activeSchemeId, scheme.id, StringComparison.Ordinal)
                            ? selectedSegmentStyle : unselectedSegmentStyle,
                        GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true)))
                    ApplyScheme(scheme.id);
                if (GUILayout.Button("删除", unselectedSegmentStyle, GUILayout.Width(60f)))
                    schemeToDelete = scheme.id;
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(schemeToDelete))
            {
                DeleteScheme(schemeToDelete);
                // The next IMGUI event must build its layout from the shorter list.
                GUIUtility.ExitGUI();
            }
        }

        private void DrawCameraPicker(CameraFormState form)
        {
            string cameraName = "手动填写地址";
            foreach (var camera in cameraCatalog.cameras)
                if (camera != null && string.Equals(camera.host, form.host.Trim(), StringComparison.OrdinalIgnoreCase))
                    cameraName = camera.name;
            if (GUILayout.Button($"选择摄像头  ·  {cameraName}", secondaryButtonStyle))
                form.cameraPickerOpen = !form.cameraPickerOpen;
            if (!form.cameraPickerOpen) return;

            form.cameraPickerScroll = GUILayout.BeginScrollView(form.cameraPickerScroll, GUILayout.Height(170f));
            foreach (var camera in cameraCatalog.cameras)
            {
                if (camera == null || !GUILayout.Button($"{camera.name}  ·  {camera.host}", unselectedSegmentStyle)) continue;
                form.host = camera.host;
                form.rtspPort = camera.rtspPort.ToString();
                form.channel = camera.channel;
                form.isSubStream = string.Equals(camera.streamType, "Sub", StringComparison.OrdinalIgnoreCase);
                form.credentialKey = camera.credentialKey;
                form.password = string.Empty;
                var provider = new WindowsCredentialManagerProvider();
                form.username = provider.TryGet(camera.credentialKey, out var credential) ? credential.Username : string.Empty;
                form.enabled = true;
                form.cameraPickerOpen = false;
            }
            GUILayout.EndScrollView();
        }

        private void RotateFeed(int index, int delta)
        {
            var form = forms[index];
            var rotation = (form.rotationDegrees + delta + 360) % 360;
            try
            {
                var document = FeedConfigurationLoader.Load(out _, out _);
                var entry = Array.Find(document.feeds, candidate => candidate != null && candidate.displayIndex == index);
                if (entry == null) throw new InvalidOperationException("Feed configuration is missing.");
                entry.rotationDegrees = rotation;
                document.activeSchemeId = string.Empty;
                FeedConfigurationLoader.Save(document);
                form.rotationDegrees = rotation;
                activeSchemeId = string.Empty;
                if (!ResolveBootstrapReference() || !bootstrap.SetFeedRotation(index, rotation))
                {
                    statusMessage = "方向已保存，重新启动程序后应用。";
                    return;
                }
                statusMessage = $"Display {index + 1} 方向已保存：顺时针 {rotation}°。";
            }
            catch (Exception ex) { statusMessage = $"保存画面方向失败：{ex.GetType().Name}"; }
        }

        private void SaveScheme()
        {
            if (string.IsNullOrWhiteSpace(schemeName))
            {
                statusMessage = "请先填写方案名称。";
                return;
            }
            var entries = new FeedConfigEntry[CameraCount];
            for (var i = 0; i < CameraCount; i++)
                if (!TryBuildFeedEntry(i, out entries[i])) return;
            try
            {
                var document = new FeedConfigurationDocument
                {
                    canvasDisplayIndex = selectedCanvasDisplayIndex,
                    feeds = entries
                };
                var saved = FeedSchemeStore.SaveOrUpdate(schemeName, document);
                schemes = FeedSchemeStore.Load(out _);
                statusMessage = $"方案“{saved.name}”已保存，点击对应方案按钮应用到四屏。";
                Debug.Log($"[HFD_SCHEME_SAVED] id={saved.id} feedCount={entries.Length}");
            }
            catch (Exception ex) { statusMessage = $"保存方案失败：{ex.GetType().Name}，原方案文件已保留。"; }
        }

        private void DeleteScheme(string id)
        {
            var removed = false;
            try
            {
                schemes = FeedSchemeStore.Load(out _);
                var selected = Array.Find(schemes.schemes, candidate => candidate != null && candidate.id == id);
                if (selected == null || !FeedSchemeStore.Delete(id))
                {
                    statusMessage = "该方案已不存在，列表已刷新。";
                    return;
                }
                removed = true;
                schemes = FeedSchemeStore.Load(out _);
                if (string.Equals(activeSchemeId, id, StringComparison.Ordinal)) activeSchemeId = string.Empty;
                if (string.Equals(schemeName, selected.name, StringComparison.Ordinal)) schemeName = string.Empty;

                var current = FeedConfigurationLoader.Load(out _, out _);
                if (string.Equals(current.activeSchemeId, id, StringComparison.Ordinal))
                {
                    current.activeSchemeId = string.Empty;
                    FeedConfigurationLoader.Save(current);
                }
                statusMessage = $"方案“{selected.name}”已删除，当前四路画面保持不变。";
                Debug.Log($"[HFD_SCHEME_DELETED] id={id} remainingCount={schemes.schemes.Length}");
            }
            catch (Exception ex)
            {
                statusMessage = removed
                    ? $"方案已删除，刷新配置标记失败：{ex.GetType().Name}；当前画面保持不变。"
                    : $"删除方案失败：{ex.GetType().Name}。";
            }
        }

        private void ApplyScheme(string id)
        {
            if (!ResolveBootstrapReference()) return;
            try
            {
                // Reload the file before every selection so stale buttons cannot silently
                // overwrite a scheme edited by another running instance.
                schemes = FeedSchemeStore.Load(out _);
                if (!FeedSchemeStore.TryGet(schemes, id, out var document))
                {
                    statusMessage = "该方案不存在，请重新打开配置。";
                    return;
                }
                document.activeSchemeId = id;
                FeedConfigurationLoader.Save(document);
                if (!bootstrap.ApplyConfiguration(document, reconnectChangedOnly: true))
                {
                    statusMessage = "方案已保存到当前配置，控制器尚未就绪，请重启程序。";
                    return;
                }
                LoadFormsFromCurrentConfig();
                var selected = Array.Find(schemes.schemes, candidate => candidate != null && candidate.id == id);
                schemeName = selected?.name ?? string.Empty;
                statusMessage = $"已切换到方案“{schemeName}”。";
                Debug.Log($"[HFD_SCHEME_APPLIED] id={id} feedCount={document.feeds.Length}");
            }
            catch (Exception ex) { statusMessage = $"切换方案失败：{ex.GetType().Name}"; }
        }

        private GUIStyle CreateButtonStyle(
            Texture2D normalBackground,
            Texture2D hoverBackground,
            Texture2D activeBackground,
            Color textColor,
            int fontSize,
            float fixedHeight,
            FontStyle fontStyle)
        {
            return new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                fixedHeight = fixedHeight,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(12, 12, 6, 7),
                border = new RectOffset(TextureCorner, TextureCorner, TextureCorner, TextureCorner),
                normal = { background = normalBackground, textColor = textColor },
                hover = { background = hoverBackground, textColor = Color.white },
                active = { background = activeBackground, textColor = Color.white },
                focused = { background = normalBackground, textColor = textColor },
                onNormal = { background = normalBackground, textColor = textColor },
                onHover = { background = hoverBackground, textColor = Color.white },
                onActive = { background = activeBackground, textColor = Color.white },
                onFocused = { background = normalBackground, textColor = textColor }
            };
        }

        private Texture2D CreateRoundedTexture(Color fill, Color border, float radius, float borderWidth)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true)
            {
                name = "HFD_RuntimeUI_RoundedRect",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[TextureSize * TextureSize];
            var halfExtent = TextureSize * 0.5f - 0.75f;
            var innerHalfExtent = Mathf.Max(0f, halfExtent - borderWidth);
            var innerRadius = Mathf.Max(0f, radius - borderWidth);

            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var px = x + 0.5f - TextureSize * 0.5f;
                    var py = y + 0.5f - TextureSize * 0.5f;
                    var outerCoverage = Mathf.Clamp01(0.5f - RoundedRectDistance(px, py, halfExtent, radius));
                    var innerCoverage = borderWidth <= 0f
                        ? outerCoverage
                        : Mathf.Clamp01(0.5f - RoundedRectDistance(px, py, innerHalfExtent, innerRadius));
                    var borderCoverage = Mathf.Max(0f, outerCoverage - innerCoverage);
                    var fillWeight = innerCoverage * fill.a;
                    var borderWeight = borderCoverage * border.a;
                    var alpha = Mathf.Clamp01(fillWeight + borderWeight);

                    if (alpha <= 0.001f)
                    {
                        pixels[y * TextureSize + x] = Color.clear;
                        continue;
                    }

                    var color = (fill * fillWeight + border * borderWeight) / alpha;
                    color.a = alpha;
                    pixels[y * TextureSize + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            generatedTextures.Add(texture);
            return texture;
        }

        private static float RoundedRectDistance(float x, float y, float halfExtent, float radius)
        {
            var qx = Mathf.Abs(x) - (halfExtent - radius);
            var qy = Mathf.Abs(y) - (halfExtent - radius);
            var outsideX = Mathf.Max(qx, 0f);
            var outsideY = Mathf.Max(qy, 0f);
            var outsideDistance = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            var insideDistance = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outsideDistance + insideDistance - radius;
        }

        private static void DrawDivider(Color color)
        {
            var rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static void DrawTopHighlight(float panelWidth)
        {
            var rect = new Rect(28f, 1f, Mathf.Max(0f, panelWidth - 56f), 2f);
            var previousColor = GUI.color;
            GUI.color = new Color32(211, 248, 255, 174);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void LoadFormsFromCurrentConfig()
        {
            configFilePath = FeedConfigurationLoader.ExternalConfigPath;

            FeedConfigurationDocument document;
            try
            {
                document = FeedConfigurationLoader.Load(out _, out _);
            }
            catch (Exception ex)
            {
                statusMessage = $"读取现有配置失败：{ex.GetType().Name}";
                return;
            }

            var credentialProvider = new WindowsCredentialManagerProvider();
            activeSchemeId = document.activeSchemeId ?? string.Empty;
            selectedCanvasDisplayIndex = document.canvasDisplayIndex >= 0 && document.canvasDisplayIndex < CameraCount
                ? document.canvasDisplayIndex
                : -1;

            for (var i = 0; i < CameraCount; i++)
            {
                var form = new CameraFormState();
                forms[i] = form;
                var feedId = $"feed{i + 1}";

                FeedConfigEntry entry = null;
                foreach (var candidate in document.feeds)
                {
                    if (candidate.displayIndex == i)
                    {
                        entry = candidate;
                        break;
                    }
                }

                if (entry != null)
                {
                    form.enabled = entry.enabled;
                    form.host = entry.host ?? string.Empty;
                    form.rtspPort = entry.rtspPort.ToString();
                    form.channel = string.IsNullOrEmpty(entry.channel) ? "101" : entry.channel;
                    form.isSubStream = string.Equals(entry.streamType, "Sub", StringComparison.OrdinalIgnoreCase);
                    form.rotationDegrees = ((entry.rotationDegrees % 360) + 360) % 360;
                }

                var credentialKey = entry == null || string.IsNullOrWhiteSpace(entry.credentialKey)
                    ? feedId
                    : entry.credentialKey;
                form.credentialKey = credentialKey;
                form.password = string.Empty;
                form.username = credentialProvider.TryGet(credentialKey, out var credential) ? credential.Username : string.Empty;
            }
        }

        private void SaveAndReconnectFeed(int index)
        {
            if (!ResolveBootstrapReference())
            {
                statusMessage = "找不到四屏控制器，配置未保存；请重新启动程序。";
                return;
            }

            if (index < 0 || index >= CameraCount)
            {
                statusMessage = "摄像头编号无效，配置未保存。";
                return;
            }

            if (!TryBuildFeedEntry(index, out var updatedEntry)) return;

            try
            {
                var document = FeedConfigurationLoader.Load(out _, out _);
                document.canvasDisplayIndex = selectedCanvasDisplayIndex;
                document.activeSchemeId = string.Empty;
                ReplaceFeedEntry(document, updatedEntry);
                FeedConfigurationLoader.Save(document);
                activeSchemeId = string.Empty;
            }
            catch (Exception ex)
            {
                statusMessage = $"保存配置失败：{ex.GetType().Name}";
                return;
            }

            Debug.Log($"[HFD_CONFIG_UI_FEED_SAVED] source=ui displayIndex={index} path={FeedConfigurationLoader.ExternalConfigPath}");
            if (!bootstrap.ReloadFeed(index))
            {
                statusMessage = $"摄像头 {index + 1} 已保存，但未找到对应显示控制器；请重新启动程序。";
                return;
            }
            var canvasStatus = selectedCanvasDisplayIndex >= 0
                ? $"Canvas 显示在 Display {selectedCanvasDisplayIndex + 1}"
                : "Canvas 已关闭";
            statusMessage = $"摄像头 {index + 1} 已保存，正在单独重新连接；{canvasStatus}。";
        }

        private bool TryBuildFeedEntry(int index, out FeedConfigEntry updatedEntry)
        {
            updatedEntry = null;

            var form = forms[index];
            var feedId = $"feed{index + 1}";

            if (form.enabled && string.IsNullOrWhiteSpace(form.host))
            {
                statusMessage = $"摄像头 {index + 1} 已启用但未填写地址。";
                return false;
            }

            if (!int.TryParse(form.rtspPort, out var port) || port <= 0 || port > 65535)
            {
                statusMessage = $"摄像头 {index + 1} 的端口无效：{form.rtspPort}";
                return false;
            }

            var credentialProvider = new WindowsCredentialManagerProvider();
            var credentialKey = string.IsNullOrWhiteSpace(form.credentialKey)
                ? feedId
                : form.credentialKey;

            if (string.IsNullOrEmpty(form.password))
            {
                if (!credentialProvider.TryGet(credentialKey, out var existingCredential))
                {
                    if (form.enabled && string.IsNullOrEmpty(form.username))
                    {
                        statusMessage = $"摄像头 {index + 1} 尚未保存凭据，请输入用户名和密码后再连接。";
                        return false;
                    }

                    if (form.enabled)
                    {
                        statusMessage = $"摄像头 {index + 1} 尚未保存密码，请输入密码后再连接。";
                        return false;
                    }
                }
                // If the username changes while several feeds share one credential, create
                // a private credential for this feed so the other feeds are not affected.
                else if (!string.Equals(form.username, existingCredential.Username, StringComparison.Ordinal))
                {
                    credentialKey = "camera-" + Guid.NewGuid().ToString("N");
                    if (!WindowsCredentialManagerProvider.Save(
                            credentialKey, form.username, existingCredential.Password))
                    {
                        statusMessage = $"摄像头 {index + 1} 的用户名更新失败，请检查日志。";
                        return false;
                    }
                }
            }
            else
            {
                // A newly entered password belongs only to this feed. This prevents a
                // password edit on one card from changing a shared credential used by
                // the other three feeds.
                if (string.IsNullOrWhiteSpace(form.username))
                {
                    statusMessage = $"摄像头 {index + 1} 未填写用户名。";
                    return false;
                }
                credentialKey = "camera-" + Guid.NewGuid().ToString("N");
                if (!WindowsCredentialManagerProvider.Save(credentialKey, form.username, form.password))
                {
                    statusMessage = $"摄像头 {index + 1} 的凭据保存失败，请检查日志。";
                    return false;
                }
                form.password = string.Empty;
            }

            form.credentialKey = credentialKey;
            updatedEntry = new FeedConfigEntry
            {
                feedId = feedId,
                displayIndex = index,
                host = form.host.Trim(),
                rtspPort = port,
                channel = string.IsNullOrWhiteSpace(form.channel) ? "101" : form.channel.Trim(),
                streamType = form.isSubStream ? "Sub" : "Main",
                credentialKey = credentialKey,
                enabled = form.enabled,
                rotationDegrees = form.rotationDegrees
            };
            return true;
        }

        private static void ReplaceFeedEntry(FeedConfigurationDocument document, FeedConfigEntry updatedEntry)
        {
            document.feeds ??= Array.Empty<FeedConfigEntry>();

            for (var i = 0; i < document.feeds.Length; i++)
            {
                if (document.feeds[i] != null && document.feeds[i].displayIndex == updatedEntry.displayIndex)
                {
                    document.feeds[i] = updatedEntry;
                    return;
                }
            }

            var expanded = new FeedConfigEntry[document.feeds.Length + 1];
            Array.Copy(document.feeds, expanded, document.feeds.Length);
            expanded[expanded.Length - 1] = updatedEntry;
            document.feeds = expanded;
        }
    }
}
