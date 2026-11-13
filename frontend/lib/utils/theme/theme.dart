import 'package:flutter/material.dart';
import 'package:seat_charter/utils/constants/colors.dart';
import 'package:seat_charter/utils/constants/sizes.dart';

/// One builder for light and dark so both stay in step. Flat surfaces, hairline borders,
/// soft radii and a single accent; screens should rarely need their own colours.
class IKAppTheme {
  IKAppTheme._();

  static const _font = 'PlusJakartaSans';

  static ThemeData lightTheme = _build(Brightness.light);
  static ThemeData darkTheme = _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final bg = dark ? IKColors.darkBackground : IKColors.background;
    final card = dark ? IKColors.darkCard : IKColors.card;
    final title = dark ? IKColors.darkTitle : IKColors.title;
    final text = dark ? IKColors.darkText : IKColors.text;
    final border = dark ? IKColors.darkBorder : IKColors.border;
    final fill = dark ? IKColors.dark : IKColors.card;

    final scheme = ColorScheme.fromSeed(seedColor: IKColors.primary, brightness: brightness).copyWith(
      primary: IKColors.primary,
      onPrimary: Colors.white,
      surface: card,
      onSurface: title,
      onSurfaceVariant: text,
      outline: border,
      outlineVariant: border,
      error: IKColors.danger,
    );

    TextStyle s(double size, FontWeight w, Color c, {double spacing = 0, double? height}) =>
        TextStyle(fontFamily: _font, fontSize: size, fontWeight: w, color: c, letterSpacing: spacing, height: height);

    final textTheme = TextTheme(
      displaySmall: s(34, FontWeight.w800, title, spacing: -1),
      headlineLarge: s(26, FontWeight.w700, title, spacing: -0.6, height: 1.2),
      headlineMedium: s(20, FontWeight.w700, title, spacing: -0.3),
      headlineSmall: s(16, FontWeight.w700, title, spacing: -0.1),
      titleLarge: s(16, FontWeight.w600, title),
      titleMedium: s(14, FontWeight.w600, title),
      titleSmall: s(12, FontWeight.w600, title),
      bodyLarge: s(16, FontWeight.w400, title, height: 1.45),
      bodyMedium: s(14, FontWeight.w400, text, height: 1.4),
      bodySmall: s(12, FontWeight.w500, text),
      labelLarge: s(15, FontWeight.w600, title),
      labelMedium: s(13, FontWeight.w600, text),
      labelSmall: s(11, FontWeight.w600, text, spacing: 0.4),
    );

    final radius = BorderRadius.circular(IKSizes.radiusLg);
    const buttonPadding = EdgeInsets.symmetric(vertical: 16, horizontal: 24);
    final buttonText = s(16, FontWeight.w600, Colors.white);

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      fontFamily: _font,
      colorScheme: scheme,
      primaryColor: IKColors.primary,
      scaffoldBackgroundColor: bg,
      canvasColor: bg,
      cardColor: card,
      dividerColor: border,
      textTheme: textTheme,
      splashFactory: InkSparkle.splashFactory,
      appBarTheme: AppBarTheme(
        elevation: 0,
        scrolledUnderElevation: 0,
        centerTitle: false,
        toolbarHeight: IKSizes.headerHeight,
        backgroundColor: bg,
        surfaceTintColor: Colors.transparent,
        foregroundColor: title,
        iconTheme: IconThemeData(color: title, size: 22),
        titleTextStyle: s(22, FontWeight.w700, title, spacing: -0.4),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          elevation: 0,
          shadowColor: Colors.transparent,
          backgroundColor: IKColors.primary,
          foregroundColor: Colors.white,
          disabledBackgroundColor: IKColors.primary.withValues(alpha: 0.35),
          disabledForegroundColor: Colors.white70,
          padding: buttonPadding,
          minimumSize: const Size(64, 54),
          textStyle: buttonText,
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: title,
          backgroundColor: card,
          side: BorderSide(color: border),
          padding: buttonPadding,
          minimumSize: const Size(64, 54),
          textStyle: buttonText.copyWith(color: title),
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: IKColors.primary, textStyle: s(15, FontWeight.w600, IKColors.primary)),
      ),
      segmentedButtonTheme: SegmentedButtonThemeData(
        style: SegmentedButton.styleFrom(
          side: BorderSide(color: border),
          selectedBackgroundColor: IKColors.primary.withValues(alpha: 0.12),
          selectedForegroundColor: IKColors.primary,
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      cardTheme: CardThemeData(
        color: card,
        elevation: 0,
        margin: EdgeInsets.zero,
        surfaceTintColor: Colors.transparent,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(IKSizes.radiusXl), side: BorderSide(color: border)),
      ),
      listTileTheme: ListTileThemeData(
        iconColor: text,
        textColor: title,
        contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 2),
        titleTextStyle: s(15, FontWeight.w600, title),
        subtitleTextStyle: s(13, FontWeight.w400, text),
      ),
      dividerTheme: DividerThemeData(color: border, thickness: 1, space: 1),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        height: 68,
        indicatorColor: IKColors.primary.withValues(alpha: dark ? 0.22 : 0.10),
        indicatorShape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(IKSizes.radiusMd + 4)),
        labelTextStyle: WidgetStateProperty.resolveWith(
            (st) => s(12, FontWeight.w600, st.contains(WidgetState.selected) ? IKColors.primary : text)),
        iconTheme: WidgetStateProperty.resolveWith(
            (st) => IconThemeData(size: 24, color: st.contains(WidgetState.selected) ? IKColors.primary : text)),
      ),
      chipTheme: ChipThemeData(
        backgroundColor: card,
        selectedColor: IKColors.primary.withValues(alpha: 0.12),
        side: BorderSide(color: border),
        labelStyle: s(13, FontWeight.w600, title),
        shape: const StadiumBorder(),
        showCheckmark: false,
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        showDragHandle: true,
        shape: const RoundedRectangleBorder(borderRadius: BorderRadius.vertical(top: Radius.circular(24))),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: dark ? IKColors.darkTitle : IKColors.title,
        contentTextStyle: s(14, FontWeight.w500, dark ? IKColors.title : Colors.white),
        shape: RoundedRectangleBorder(borderRadius: radius),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: IKColors.primary, linearTrackColor: border),
      inputDecorationTheme: _inputs(fill: fill, focus: IKColors.primary, text: text, border: border),
    );
  }

  /// Settings → high contrast: pure black/white text, visible borders everywhere.
  static ThemeData highContrast(ThemeData base) {
    final dark = base.brightness == Brightness.dark;
    final fg = dark ? Colors.white : Colors.black;
    return base.copyWith(
      textTheme: base.textTheme.apply(bodyColor: fg, displayColor: fg),
      dividerColor: fg,
      cardTheme: base.cardTheme.copyWith(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(IKSizes.radiusXl), side: BorderSide(color: fg, width: 1.5))),
      inputDecorationTheme:
          _inputs(fill: dark ? Colors.black : Colors.white, focus: IKColors.primary, text: fg, border: fg, width: 2),
    );
  }

  static InputDecorationTheme _inputs({required Color fill, required Color focus, required Color text, Color? border, double width = 1.5}) {
    OutlineInputBorder side(Color c, [double? w]) =>
        OutlineInputBorder(borderSide: BorderSide(color: c, width: w ?? width), borderRadius: BorderRadius.circular(IKSizes.radiusLg));
    return InputDecorationTheme(
      filled: true,
      fillColor: fill,
      hintStyle: TextStyle(fontFamily: _font, color: text, fontWeight: FontWeight.w400),
      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
      border: side(border ?? fill),
      enabledBorder: side(border ?? fill),
      focusedBorder: side(focus, 2),
      errorBorder: side(IKColors.danger),
      focusedErrorBorder: side(IKColors.danger, 2),
    );
  }
}
