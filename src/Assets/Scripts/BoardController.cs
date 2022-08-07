using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct FallData
{
    public readonly int X { get; }
    public readonly int Y { get; }
    public readonly int Dest { get; } // 落ちる先
    public FallData(int x, int y, int dest)
    {
        X = x;
        Y = y;
        Dest = dest;
    }
}

public class BoardController : MonoBehaviour
{
    public const int FALL_FRAME_PER_CELL = 5;// 単位セル当たりの落下フレーム数
    public const int BOARD_WIDTH = 6;
    public const int BOARD_HEIGHT = 14;

    [SerializeField] GameObject prefabPuyo = default!;

    int[,] _board = new int[BOARD_HEIGHT, BOARD_WIDTH];
    GameObject[,] _Puyos = new GameObject[BOARD_HEIGHT, BOARD_WIDTH];

    uint _additiveScore = 0;

    // 落ちる際の一次的変数
    List<FallData> _falls = new();
    int _fallFrames = 0;

    // 削除する際の一次的変数
    List<Vector2Int> _erases = new();
    int _eraseFrames = 0;

    private void ClearAll()
    {
        for (int y = 0; y < BOARD_HEIGHT; y++)
        {
            for (int x = 0; x < BOARD_WIDTH; x++)
            {
                _board[y, x] = 0;

                if (_Puyos[y, x] != null) Destroy(_Puyos[y, x]);
                _Puyos[y, x] = null;
            }
        }
    }

    public void Start()
    {
        ClearAll();
    }

    public static bool IsValidated(Vector2Int pos)
    {
        return 0 <= pos.x && pos.x < BOARD_WIDTH
            && 0 <= pos.y && pos.y < BOARD_HEIGHT;
    }

    public bool CanSettle(Vector2Int pos)
    {
        if (!IsValidated(pos)) return false;

        return 0 == _board[pos.y, pos.x];
    }

    public bool Settle(Vector2Int pos, int val)
    {
        if (!CanSettle(pos)) return false;

        _board[pos.y, pos.x] = val;

        Debug.Assert(_Puyos[pos.y, pos.x] == null);// 念のための確認
        Vector3 world_position = transform.position + new Vector3(pos.x, pos.y, 0.0f);
        _Puyos[pos.y, pos.x] = Instantiate(prefabPuyo, world_position, Quaternion.identity, transform);
        _Puyos[pos.y, pos.x].GetComponent<PuyoController>().SetPuyoType((PuyoType)val);

        return true;
    }

    // 下が空間となっていて落ちるぷよを検索する
    public bool CheckFall()
    {
        _falls.Clear();
        _fallFrames = 0;

        // 落ちる先の高さの記録用
        int[] dsts = new int[BOARD_WIDTH];
        for (int x = 0; x < BOARD_WIDTH; x++) dsts[x] = 0;

        int max_check_line = BOARD_HEIGHT - 1;// 実はぷよぷよ通では最上段は落ちてこない
        for (int y = 0; y < max_check_line; y++)// 下から上に検索
        {
            for (int x = 0; x < BOARD_WIDTH; x++)
            {
                if (_board[y, x] == 0) continue;

                int dst = dsts[x];
                dsts[x] = y + 1;// 上のぷよが落ちてくるなら自分の上

                if (y == 0) continue;// 一番下なら落ちない

                if (_board[y - 1, x] != 0) continue;// 下があれば対象外

                _falls.Add(new FallData(x, y, dst));

                // データを変更しておく
                _board[dst, x] = _board[y, x];
                _board[y, x] = 0;
                _Puyos[dst, x] = _Puyos[y, x];
                _Puyos[y, x] = null;

                dsts[x] = dst + 1;// 次の物は落ちたさらに上に乗る
            }
        }

        return _falls.Count != 0;
    }

    public bool Fall()
    {
        _fallFrames++;

        float dy = _fallFrames / (float)FALL_FRAME_PER_CELL;
        int di = (int)dy;

        for (int i = _falls.Count - 1; 0 <= i; i--)// ループ中で削除しても安全なように後ろから検索
        {
            FallData f = _falls[i];

            Vector3 pos = _Puyos[f.Dest, f.X].transform.localPosition;
            pos.y = f.Y - dy;

            if (f.Y <= f.Dest + di)
            {
                pos.y = f.Dest;
                _falls.RemoveAt(i);
            }
            _Puyos[f.Dest, f.X].transform.localPosition = pos;// 表示位置の更新
        }

        return _falls.Count != 0;
    }

    // ボーナス計算用のテーブル
    static readonly uint[] chainBonusTbl = new uint[] {
        0, 8, 16, 32, 64,
        96, 128, 160, 192, 224,
        256, 288, 320, 352, 384,
        416, 448, 480, 512 };

    static readonly uint[] connectBonusTbl = new uint[] {
        0, 0, 0, 0, 0, 2, 3, 4, 5, 6, 7,
    };

    static readonly uint[] colorBonusTbl = new uint[] {
        0, 3, 6, 12, 24,
    };

    static readonly Vector2Int[] search_tbl = new Vector2Int[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    // 消えるぷよを検索する（同じ色を上下左右に見つけていく。見つけたらフラグを立てて再計算しない）
    public bool CheckErase(int chainCount)
    {
        _eraseFrames = 0;
        _erases.Clear();

        uint[] isChecked = new uint[BOARD_HEIGHT];// メモリを多く使うのは無駄なのでビット処理

        // 得点計算用
        int puyoCount = 0;
        uint colorBits = 0;
        uint connectBonus = 0;

        List<Vector2Int> add_list = new();
        for (int y = 0; y < BOARD_HEIGHT; y++)
        {
            for (int x = 0; x < BOARD_WIDTH; x++)
            {
                if ((isChecked[y] & (1u << x)) != 0) continue;// 調査済み

                isChecked[y] |= (1u << x);

                int type = _board[y, x];
                if (type == 0) continue;// 空間だった

                puyoCount++;

                System.Action<Vector2Int> get_connection = null;// 再帰で使う場合に必要
                get_connection = (pos) =>
                {
                    add_list.Add(pos);// 削除対象とする

                    foreach (Vector2Int d in search_tbl)
                    {
                        Vector2Int target = pos + d;
                        if (target.x < 0 || BOARD_WIDTH <= target.x ||
                            target.y < 0 || BOARD_HEIGHT <= target.y) continue;// 範囲外
                        if (_board[target.y, target.x] != type) continue;// 色違い
                        if ((isChecked[target.y] & (1u << target.x)) != 0) continue;// 検索済み

                        isChecked[target.y] |= (1u << target.x);
                        get_connection(target);
                    }
                };

                add_list.Clear();
                get_connection(new Vector2Int(x, y));

                if (4 <= add_list.Count)
                {
                    connectBonus += connectBonusTbl[System.Math.Min(add_list.Count, connectBonusTbl.Length - 1)];
                    colorBits |= (1u << type);
                    _erases.AddRange(add_list);
                }
            }
        }

        if (chainCount != -1)// 初期化時は得点計算はしない
        {
            // ボーナス計算
            uint colorNum = 0;
            for (; 0 < colorBits; colorBits >>= 1)// 立っているビットの数を数える
            {
                colorNum += (colorBits & 1u);
            }

            uint colorBonus = colorBonusTbl[System.Math.Min(colorNum, colorBonusTbl.Length - 1)];
            uint chainBonus = chainBonusTbl[System.Math.Min(chainCount, chainBonusTbl.Length - 1)];
            uint bonus = System.Math.Max(1, chainBonus + connectBonus + colorBonus);// 0 の時も1入る
            _additiveScore += 10 * (uint)_erases.Count * bonus;

            if (puyoCount == 0) _additiveScore += 1800;// 全消しボーナス
        }

        return _erases.Count != 0;
    }

    public bool Erase()
    {
        _eraseFrames++;

        // 1から増えてちょっとしたら最大に大きくなったあと小さくなって消える
        float t = _eraseFrames * Time.deltaTime;
        t = 1.0f - 10.0f * ((t - 0.1f) * (t - 0.1f) - 0.1f * 0.1f);

        // 大きさが負ならおしまい
        if (t <= 0.0f)
        {
            // データとゲームオブジェクトをここで消す
            foreach (Vector2Int d in _erases)
            {
                Destroy(_Puyos[d.y, d.x]);
                _Puyos[d.y, d.x] = null;
                _board[d.y, d.x] = 0;
            }

            return false;
        }

        // モデルの大きさを変える
        foreach (Vector2Int d in _erases)
        {
            _Puyos[d.y, d.x].transform.localScale = Vector3.one * t;
        }

        return true;
    }

    // 得点の受け渡し
    public uint popScore()
    {
        uint score = _additiveScore;
        _additiveScore = 0;

        return score;
    }
}