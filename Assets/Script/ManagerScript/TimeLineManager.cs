using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
public class TimeLineManager : MonoBehaviour
{
    //再生するタイムラインのステータスを宣言
    public enum TimeLineState
    {
        Setting,
        Reverse,
        MainGame,
        Battle,
        ManualStart,
        ManualEnd,
        None
    }
    public static TimeLineManager Instance { get; private set; }

    //一つしかないのを保証
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [Serializable]
    public class TimeLines
    {
        public TimeLineState State;
        public PlayableDirector Timeline;
     }

    public List<TimeLines> DList;//各シーンで流すタイムラインを設定する

    private Dictionary<TimeLineState, PlayableDirector> timeLineList = new();//リストに設定した情報をディクショナリー

    private void Start()
    {
        InitList();
    }
    private void InitList()//ディクショナリーにリストの内容を設定する
    {
        ////リストに設定した情報をディクショナリーに設定
        foreach (var line in DList)
        {
            if (line.Timeline == null) continue;
            timeLineList[line.State] = line.Timeline;
        }
    }

    //引数にあったタイムラインを捜して再生する
    public void PlayTimeLine(TimeLineState state)
    {
        var timeline = GetTimeline(state);//紐付けられたtimelineを取得
        if (timeline == null) return;

        timeline.Play();//再生
    }
    private PlayableDirector GetTimeline(TimeLineState state)
    {
        //引数で渡されたタイプが登録されていたらtimelineを返す
        if(timeLineList.TryGetValue(state,out var timeline))
        {
            return timeline;
        }
        return null;
    }
   
}
