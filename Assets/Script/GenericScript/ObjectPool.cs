using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[Serializable]
public class AddressableObjectPool<T>where T:Enum
{
    [Serializable]
    public class Entry
    {
        public T Key;
        public AssetReferenceT<GameObject> Value;//生成元のオブジェクト
        public int InstantiateCount;//何個生成するか
    }

    [SerializeField] private List<Entry> entryList;

    //非同期でロードが完了したものを保管するリスト
    private Dictionary<T, AsyncOperationHandle<GameObject>> loadedHandleList = new();

    //生成したものを保管するリスト
    private Dictionary<T, Queue<GameObject>> cacheList = new();
    public async Task Init(Transform parent = null)
    {
        //リストが無かったら新しく生成
        loadedHandleList ??= new();
        cacheList ??= new();

        //中身を空にする
        loadedHandleList.Clear();
        cacheList.Clear();

        foreach (var entry in entryList)
        {
            //ゲームオブジェクトを非同期でロード
            var handle = entry.Value.LoadAssetAsync<GameObject>();
            
            //ロード中
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)//例外が発生したら戻す
                continue;

            loadedHandleList[entry.Key] = handle;//完成したら生成元を保管

            var queue = new Queue<GameObject>();

            //指定した回数分生成する
            for (int i = 0; i < entry.InstantiateCount; i++)
            {
                var instance = GameObject.Instantiate(handle.Result,parent);
                instance.SetActive(false);

                queue.Enqueue(instance);
            }

            cacheList[entry.Key] = queue;
        }
    }

    public GameObject Get(T state)
    {

        if (!cacheList.TryGetValue(state, out var queue))
            return null;

        if (queue.Count > 0)
        {
            var obj = queue.Dequeue();
            obj.SetActive(true);
            return obj;
        }

        return null;
    }
    public GameObject GetPrefab(T state)
    {
        //生成元を取得
      if (loadedHandleList.TryGetValue(state, out var obj))
        {
            return obj.Result;
        }
        return null;

    }
    public void Return(T state, GameObject obj)//エフェクトを返す
    {

        obj.SetActive(false);

        cacheList[state].Enqueue(obj);
    }
    /// <summary>
    /// キャッシュされているオブジェクトを消したり、メモリを解放する
    /// </summary>
    public void Release()//シーンを移動したり、オブジェクトが消えたらよばれる
    {

        foreach (var queue in cacheList.Values)
        {
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();

                if (obj != null)
                {
                    GameObject.Destroy(obj);
                }
            }
        }

        foreach (var handle in loadedHandleList.Values)
        {
            Addressables.Release(handle);
        }

        cacheList.Clear();
        loadedHandleList.Clear();
    }
}