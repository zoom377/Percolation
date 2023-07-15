using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class Percolation : MonoBehaviour
{
    [SerializeField] Renderer _renderer;
    [SerializeField] int _width = 256, _height = 256;
    [SerializeField] float _pValue = 0;
    [SerializeField] float _rate;
    Texture2D _texture;
    Node[] _nodes;



    void Start()
    {
        _texture = new Texture2D(_width, _height);
        _nodes = new Node[_width * _height];
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new Node { BottomChance = Random.Range(0f, 1f), RightChance = Random.Range(0f, 1f) };
        }

        StartCoroutine(UpdateTexture());
        
    }

    IEnumerator UpdateTexture()
    {
        while (_pValue <= 1f)
        {
            _pValue += _rate;
            UpdateLinkOpenness();
            GenerateTexture();
            yield return new WaitForSeconds(.1f);
        }
    }

    void GenerateTexture()
    {
        var sw = Stopwatch.StartNew();

        //Reset visited status of all nodes
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].Visited = false;
        }

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (false == GetNode(new Vector2Int(x, y)).Visited)
                {
                    FloodFill(_texture, new Vector2Int(x, y), GetRandomColor());
                }
            }
        }

        _texture.Apply();
        _renderer.material.mainTexture = _texture;
        Debug.Log($"GenerateTexture: {sw.ElapsedMilliseconds}");
    }

    void FloodFill(Texture2D texture, Vector2Int startPos, Color color)
    {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        stack.Push(startPos);

        Vector2Int pos, adj;
        while (stack.TryPop(out pos))
        {
            GetNode(pos).Visited = true;
            texture.SetPixel(pos.x, pos.y, color);

            adj = pos + Vector2Int.left;
            if (adj.x >= 0 &&
                false == GetNode(adj).Visited &&
                GetNode(adj).RightOpen)
            {
                //Left is a node
                stack.Push(adj);
            }

            adj = pos + Vector2Int.right;
            if (adj.x < _width &&
                false == GetNode(adj).Visited &&
                GetNode(pos).RightOpen)
            {
                //Left is a node
                stack.Push(adj);
            }

            adj = pos + Vector2Int.down;
            if (adj.y >= 0 &&
                false == GetNode(adj).Visited &&
                GetNode(pos).BottomOpen)
            {
                //Left is a node
                stack.Push(adj);
            }

            adj = pos + Vector2Int.up;
            if (adj.y < _height &&
                false == GetNode(adj).Visited &&
                GetNode(adj).BottomOpen)
            {
                //Left is a node
                stack.Push(adj);
            }
        }
    }

    void UpdateLinkOpenness()
    {
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].RightOpen = _nodes[i].RightChance < _pValue ? true : false;
            _nodes[i].BottomOpen = _nodes[i].BottomChance < _pValue ? true : false;
        }

        Debug.Log($"UpdateLinkOpenness: {sw.ElapsedMilliseconds}");
    }

    ref Node GetNode(Vector2Int pos)
    {
        return ref _nodes[Array2DToIndex(pos, _width)];
    }

    static int Array2DToIndex(Vector2Int position, int width)
    {
        return position.y * width + position.x;
    }

    static Color GetRandomColor()
    {
        return new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            1f
            );
    }


    struct Node
    {
        public bool Visited;
        public float RightChance;
        public float BottomChance;
        public bool RightOpen;
        public bool BottomOpen;
    }
}
