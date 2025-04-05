using UnityEngine;
using UnityEditor;
using System.IO;

public class FlowTextureGenerator : EditorWindow
{
    // ======== テクスチャサイズ設定 ========
    [Header("テクスチャサイズ")]
    private int _width = 512;                    // 生成するテクスチャの幅（ピクセル数）
    private int _height = 512;                   // 生成するテクスチャの高さ（ピクセル数）

    // ======== フラクタルノイズ設定 ========
    [Header("フラクタルノイズ設定")]
    private int _octaves = 4;                    // フラクタルノイズのオクターブ数（多いほど複雑なパターンに）
    private float _scaleX = 8f;                  // X方向のノイズスケール。大きいとノイズパターンが横に広がる
    private float _scaleY = 2f;                  // Y方向のノイズスケール。小さいと横方向に流れるような模様に
    private float _offsetX = 0f;                 // X方向オフセット。ノイズパターン全体を左右にシフトする
    private float _offsetY = 0f;                 // Y方向オフセット。ノイズパターン全体を上下にシフトする
    private float _lacunarity = 2.0f;            // 各オクターブごとの周波数倍率（通常は2）
    private float _gain = 0.5f;                  // 各オクターブごとの振幅減衰率（通常は0.5）

    // ======== 方向性ブラー設定 ========
    [Header("方向性ブラー設定")]
    private bool _applyDirectionalBlur = true;   // 方向性ブラーを適用するかどうか
    private int _blurStrength = 3;               // ブラー強度。各ピクセルから±この範囲でサンプルする（ピクセル数）
    private float _blurAngle = 0f;               // ブラー角度（度数法：0°は水平、90°は垂直）

    // ======== パラメータ説明表示 ========
    private bool _showExplanations = false;      // パラメータ説明の表示/非表示切替用フラグ
    private Vector2 _scrollPosition;             // スクロールビュー用のスクロール位置
    private Texture2D _previewTexture;           // プレビュー用テクスチャ（ウィンドウ内表示用）

    [MenuItem("MyTools/flowTextureGeneratorフローテクスチャ生成")]
    public static void ShowWindow()
    {
        GetWindow<FlowTextureGenerator>("フローテクスチャ生成");
    }

    void OnGUI()
    {
        // 全体をスクロールビューでラップ
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        GUILayout.Label("=== フローテクスチャ設定 ===", EditorStyles.boldLabel);

        // パラメータ説明表示のオン/オフ
        _showExplanations = EditorGUILayout.Toggle("パラメータ説明を表示", _showExplanations);

        // テクスチャサイズの設定
        _width = EditorGUILayout.IntField("幅", _width);
        _height = EditorGUILayout.IntField("高さ", _height);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("生成するテクスチャの解像度です。解像度が高いほど細かい模様が表現できますが、生成に時間がかかる場合があります。", MessageType.Info);
        }

        GUILayout.Space(5);
        GUILayout.Label("【フラクタルノイズ設定】", EditorStyles.boldLabel);
        _octaves = EditorGUILayout.IntField("オクターブ数", _octaves);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("オクターブ数が多いほど、より複雑なノイズが生成されます。", MessageType.Info);
        }
        _scaleX = EditorGUILayout.FloatField("X方向スケール", _scaleX);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("X方向のスケール。大きい値にすると、ノイズパターンが横方向に広がります。", MessageType.Info);
        }
        _scaleY = EditorGUILayout.FloatField("Y方向スケール", _scaleY);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("Y方向のスケール。小さい値にすると、横に流れるような模様になります。", MessageType.Info);
        }
        _offsetX = EditorGUILayout.FloatField("Xオフセット", _offsetX);
        _offsetY = EditorGUILayout.FloatField("Yオフセット", _offsetY);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("オフセット値を変更することで、ノイズパターン全体の開始位置をシフトし、異なる模様を得ることができます。", MessageType.Info);
        }
        _lacunarity = EditorGUILayout.FloatField("Lacunarity", _lacunarity);
        _gain = EditorGUILayout.FloatField("Gain", _gain);
        if (_showExplanations)
        {
            EditorGUILayout.HelpBox("lacunarityは各オクターブでの周波数倍率、gainは振幅の減衰率です。これにより、ノイズの細かさや強度が変化します。", MessageType.Info);
        }

        GUILayout.Space(5);
        GUILayout.Label("【方向性ブラー設定】", EditorStyles.boldLabel);
        _applyDirectionalBlur = EditorGUILayout.Toggle("方向性ブラーを適用", _applyDirectionalBlur);
        if (_applyDirectionalBlur)
        {
            _blurAngle = EditorGUILayout.FloatField("ブラー角度 (°)", _blurAngle);
            if (_showExplanations)
            {
                EditorGUILayout.HelpBox("ブラーの方向を角度で指定します。0°は水平（左から右）、90°は垂直（下から上）になります。", MessageType.Info);
            }
            _blurStrength = EditorGUILayout.IntSlider("ブラー強度 (px)", _blurStrength, 1, 20);
            if (_showExplanations)
            {
                EditorGUILayout.HelpBox("ブラー強度です。値が大きいほど、各ピクセル周辺からより多くのサンプルを平均してブラー効果が強くなります。", MessageType.Info);
            }
        }

        GUILayout.Space(10);
        if (GUILayout.Button("プレビュー更新"))
        {
            UpdatePreviewTexture();
        }

        GUILayout.Space(10);
        if (_previewTexture != null)
        {
            GUILayout.Label("プレビュー:");
            float previewWidth = EditorGUIUtility.currentViewWidth - 40;
            float aspect = (float)_height / _width;
            float previewHeight = previewWidth * aspect;
            GUILayout.Label(_previewTexture, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
        }
        else
        {
            GUILayout.Label("プレビューはまだ生成されていません。");
        }

        GUILayout.Space(10);
        if (GUILayout.Button("生成して保存"))
        {
            GenerateAndSaveTexture();
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 現在のパラメータでプレビュー用テクスチャを生成する
    /// 1) フラクタルノイズを用いてベーステクスチャを生成
    /// 2) 必要に応じて方向性ブラーを適用
    /// </summary>
    void UpdatePreviewTexture()
    {
        Texture2D baseTex = GenerateFractalNoiseTexture(_width, _height);
        if (_applyDirectionalBlur)
        {
            _previewTexture = ApplyDirectionalBlur(baseTex, _blurAngle, _blurStrength);
        }
        else
        {
            _previewTexture = baseTex;
        }
    }

    /// <summary>
    /// プレビュー画像をPNG形式で保存する
    /// </summary>
    void GenerateAndSaveTexture()
    {
        if (_previewTexture == null)
        {
            UpdatePreviewTexture();
        }
        string path = EditorUtility.SaveFilePanel("フローテクスチャの保存", Application.dataPath, "flow_texture", "png");
        if (!string.IsNullOrEmpty(path))
        {
            byte[] pngData = _previewTexture.EncodeToPNG();
            File.WriteAllBytes(path, pngData);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("保存完了", "テクスチャが保存されました:\n" + path, "OK");
        }
    }

    /// <summary>
    /// フラクタルノイズのテクスチャを生成する
    /// 各ピクセルの色は複数オクターブのノイズの重ね合わせで決定
    /// </summary>
    Texture2D GenerateFractalNoiseTexture(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float noiseVal = FractalNoise(x, y);
                Color color = new Color(noiseVal, noiseVal, noiseVal, 1f);
                tex.SetPixel(x, y, color);
            }
        }
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// フラクタルノイズを計算する
    /// 各オクターブで異なるスケールと振幅でPerlinノイズを重ね合わせる
    /// </summary>
    float FractalNoise(int x, int y)
    {
        float amplitude = 1f;
        float frequency = 1f;
        float sum = 0f;
        float max = 0f;
        for (int i = 0; i < _octaves; i++)
        {
            float sampleX = (_offsetX + ((float)x / _width) * _scaleX) * frequency;
            float sampleY = (_offsetY + ((float)y / _height) * _scaleY) * frequency;
            float val = Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
            sum += val;
            max += amplitude;
            amplitude *= _gain;
            frequency *= _lacunarity;
        }
        return sum / max;
    }

    /// <summary>
    /// 方向性ブラーを適用する
    /// angle: ブラーの方向（度数法、0°は水平）
    /// strength: 各ピクセルからこの範囲でサンプリングして平均値を計算（ピクセル数）
    /// </summary>
    Texture2D ApplyDirectionalBlur(Texture2D source, float angle, int strength)
    {
        int w = source.width;
        int h = source.height;
        Texture2D result = new Texture2D(w, h, source.format, false);
        Color[] srcPixels = source.GetPixels();
        Color[] dstPixels = new Color[srcPixels.Length];
        float rad = angle * Mathf.Deg2Rad;
        float dx = Mathf.Cos(rad);
        float dy = Mathf.Sin(rad);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color accum = Color.black;
                int count = 0;
                for (int s = -strength; s <= strength; s++)
                {
                    int sx = Mathf.RoundToInt(x + dx * s);
                    int sy = Mathf.RoundToInt(y + dy * s);
                    if (sx >= 0 && sx < w && sy >= 0 && sy < h)
                    {
                        accum += srcPixels[sy * w + sx];
                        count++;
                    }
                }
                dstPixels[y * w + x] = (count > 0) ? accum / count : srcPixels[y * w + x];
            }
        }
        result.SetPixels(dstPixels);
        result.Apply();
        return result;
    }
}
