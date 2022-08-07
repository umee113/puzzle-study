using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PuyoType
{
    Blank = 0,

    Green = 1,
    Red = 2,
    Yellow = 3,
    Blue = 4,
    Purple = 5,
    Cyan = 6,

    Invalid = 7,
};

[RequireComponent(typeof(Renderer))]
public class PuyoController : MonoBehaviour
{
    static readonly Color[] color_table = new Color[] {
        Color.black,

        Color.green,
        Color.red,
        Color.yellow,
        Color.blue,
        Color.magenta,
        Color.cyan,

        Color.gray,
    };

    [SerializeField] Renderer my_renderer = default!;// 自分自身のマテリアルを登録しておく(GetComponentをなくす)
    PuyoType _type = PuyoType.Invalid;

    // Start is called before the first frame update
    //    void Start()
    //    {
    //        // 今回は使わない
    //    }

    // Update is called once per frame
    //    void Update()
    //    {
    //        // 今回は使わない
    //    }

    public void SetPuyoType(PuyoType type)
    {
        _type = type;

        my_renderer.material.color = color_table[(int)_type];
    }
    public PuyoType GetPuyoType()
    {
        return _type;
    }

    public void SetPos(Vector3 pos)
    {
        this.transform.localPosition = pos;
    }
}