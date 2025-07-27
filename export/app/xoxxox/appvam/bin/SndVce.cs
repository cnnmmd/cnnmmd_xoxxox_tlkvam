using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Net.Sockets;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xoxxox;

namespace Xoxxox {
  public class SndVce: MVRScript {

    // 変数を定義
    public string srvadr = Params.srvadr; // サーバのアドレス
    public int srvprt = Params.srvprt; // サーバのポート
    public string pthrcv = ParamsVce.pthrcv; // サーバのパス（受信用）
    public int ratsmp = 16000; // 音声のサンプル数／秒＝サンプルレート
    public int secrec = 2; // 音声を録音する単位時間（秒）
    public float vcehld = 0.01f; // 録音を開始する音量の閾値
    public int mscrep = 100; // スレッドを待機させる時間（マイクロ秒）

    private Thread trdbak; // スレッド（バックグラウンド）
    private AudioClip objmic; // デバイス（マイクロフォン）
    private Queue<List<float[]>> quevce = new Queue<List<float[]>>(); // 音声データのキュー
    private bool flgrun = true; // コンポーネントの状態

    // 処理を開始
    private void Start() {
      objmic = Microphone.Start(Microphone.devices[0], true, secrec, ratsmp);
      StartCoroutine(RecVce()); // 反復を開始（音声を録音（スレッド：フォアグラウンド：コルーチン））
      trdbak = new Thread(PolVce); // 反復を開始（音声を送信（スレッド：バックグラウンド））
      trdbak.Start();
    }

    // 音声を録音（スレッド：フォアグラウンド：コルーチン）
    private IEnumerator RecVce() {
      float[] arrvce = new float[secrec * ratsmp]; // 録音した音声を格納するアレイ
      List<float[]> lstvce = new List<float[]>(); // 音声アレイを格納するリスト
      while (true) {
        objmic.GetData(arrvce, 0); // 最新の音声を取得
        float vcemax = MaxVal(arrvce); // 最大の音声を取得
        if (vcemax > vcehld) { // 音声が閾値を超えたなら
          SuperController.LogMessage("AddVce[]"); // DBG
          lstvce.Add((float[])arrvce.Clone()); // 音声アレイを音声リストに追加
        }
        else if (lstvce.Count > 0) { // 音声が連続して閾値未満なら
          SuperController.LogMessage("SndVce[]"); // DBG
          List<float[]> lstvce_cpy = new List<float[]>(); // 音声リストの複製を作成
          foreach (float[] v in lstvce) {
            lstvce_cpy.Add((float[])v.Clone()); // 音声リストの複製に追加
          }
          lock (quevce) {
            quevce.Enqueue(lstvce_cpy); // 音声リストの複製をキューに格納
          }
          lstvce.Clear(); // 音声リストをリセット
        }
        yield return new WaitForSeconds((float)secrec); // 録音時間だけ待機
      }
    }

    // 最大を取得
    private float MaxVal(float[] arrflt) {
      float fltmax = 0;
      foreach (float f in arrflt) {
        if (Mathf.Abs(f) > fltmax) {
          fltmax = Mathf.Abs(f);
        }
      }
      return fltmax;
    }

    // 音声を送信（スレッド：バックグラウンド）
    private void PolVce() {
      while (flgrun) {
        List<float[]> lstvce = null;
        lock (quevce) {
          if (quevce.Count > 0) { // キューが空でないなら
            lstvce = quevce.Dequeue(); // キューから音声リストの複製を取得
          }
        }
        if (lstvce != null) {
          byte[] arrvce = CnvByt(lstvce);
          using (TcpClient client = new TcpClient(srvadr, srvprt))
          using (NetworkStream stream = client.GetStream()) {
            string strreq =
              $"POST {pthrcv} HTTP/1.1\r\n" +
              $"Host: {srvadr}\r\n" +
              "Content-Type: application/octet-stream\r\n" +
              $"Content-Length: {arrvce.Length}\r\n" +
              "Connection: close\r\n\r\n";
            byte[] bytreq = Encoding.ASCII.GetBytes(strreq);
            stream.Write(bytreq, 0, bytreq.Length);
            stream.Write(arrvce, 0, arrvce.Length);
          }
        }
        Thread.Sleep(mscrep); // 一定時間だけ待機
      }
    }

    // 音声を変換
    private byte[] CnvByt(List<float[]> lstvce) {
      float[] arrflt = lstvce.SelectMany(arrvce => arrvce).ToArray();
      byte[] arrbyt = new byte[arrflt.Length * 4];
      Buffer.BlockCopy(arrflt, 0, arrbyt, 0, arrbyt.Length);
      return arrbyt;
    }

    // 処理を終了
    private void OnDestroy() {
      flgrun = false;
      if (trdbak != null && trdbak.IsAlive) {
        trdbak.Abort();
      }
    }
  }
}
