using System;
using System.Collections.Generic;
using DG.Tweening;
using XLua;

namespace Manager {
    /// <summary>
    /// 让Lua能调用DOTween的扩展方法 / Lets Lua call DOTween's extension methods.
    ///
    /// Lua calls like `self.canvasGroup:DOFade(1, 0.5)` fail with "attempt to call a nil value"
    /// even though CanvasGroup itself resolves fine - DOFade is a C# EXTENSION method (declared on
    /// DOTweenModuleUI/ShortcutExtensions, not on CanvasGroup/Transform themselves), and extension
    /// methods aren't visible to plain reflection off the instance's own type.
    ///
    /// This project's API Compatibility Level is .NET Standard, which compiles out xLua's
    /// Reflection.Emit-based dynamic type wrapping (see NET_STANDARD_2_0 guards in
    /// ObjectTranslator.cs), so every type not explicitly code-generated is resolved through
    /// Utils.ReflectionWrap at runtime. That path *can* resolve extension methods
    /// (Utils.GetExtensionMethodsOf), but only for classes it is told to scan - hence this list.
    /// [ReflectionUse] works both in the Editor and in builds, unlike [LuaCallCSharp] alone.
    ///
    /// No xLua code regeneration needed: this is a runtime reflection lookup, not the codegen path.
    /// </summary>
    public static class LuaReflectionConfig {
        [ReflectionUse]
        public static List<Type> ReflectionUse = new List<Type>() {
            typeof(ShortcutExtensions), // DOTween.dll core: Transform.DOScale/DOScaleY, Camera/Material/Light.DOColor, ...
            typeof(DOTweenModuleUI),    // DOTween.Modules.dll: CanvasGroup/Image/Text/RectTransform/Slider/ScrollRect tweens
        };
    }
}
