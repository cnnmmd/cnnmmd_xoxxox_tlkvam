using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using Xoxxox;

namespace Xoxxox {
  public class SndAct: MVRScript {

    // 変数を定義
    public string srvadr = Params.srvadr; // サーバのアドレス
    public int srvprt = Params.srvprt; // サーバのポート
    public string pthrcv = ParamsAct.pthrcv; // サーバのパス（受信用）
    public int mscrep = 100; // スレッドを待機させる時間（マイクロ秒）

    private Thread trdbak; // スレッド（バックグラウンド）
    private bool flgrun = true; // コンポーネントの状態
    private JSONStorableString strrcv; // 入力欄（サーバのパス（受信用））
    private JSONStorableBool flgbtn; // ボタン（オン／オフ）

    // 処理を開始
    private void Start() {
      // 作成：ＵＩ：入力欄（サーバのパス（受信用））
      strrcv = new JSONStorableString("pthrcv", pthrcv);
      RegisterString(strrcv);
      var t = CreateTextField(strrcv);
      t.height = 0.25f;
      var i = t.gameObject.AddComponent<InputField>();
      i.textComponent = t.UItext;
      strrcv.inputField = i;
      // 作成：ＵＩ：ボタン（オン／オフ）
      flgbtn = new JSONStorableBool("flgbtn", false);
      RegisterBool(flgbtn);
      CreateToggle(flgbtn);
      // 作成：スレッド
      trdbak = new Thread(PolAct);
      trdbak.Start();
    }

    // 空データを送信（スレッド：バックグラウンド）
    private void PolAct() {
      while (flgrun) {
        if (flgbtn.val == true) {
          SuperController.LogMessage("SndAct:bgn[]"); // DBG
          byte[] arrcon = new byte[0];
          using (TcpClient client = new TcpClient(srvadr, srvprt))
          using (NetworkStream stream = client.GetStream()) {
            pthrcv = strrcv.val;
            string strreq =
              $"POST {pthrcv} HTTP/1.1\r\n" +
              $"Host: {srvadr}\r\n" +
              "Content-Type: application/octet-stream\r\n" +
              $"Content-Length: {arrcon.Length}\r\n" +
              "Connection: close\r\n\r\n";
            byte[] bytreq = Encoding.ASCII.GetBytes(strreq);
            stream.Write(bytreq, 0, bytreq.Length);
            stream.Write(arrcon, 0, arrcon.Length);
            //
            flgbtn.val = false;
          }
          SuperController.LogMessage("SndAct:end[]"); // DBG
        }
        Thread.Sleep(mscrep); // 一定時間だけ待機
      }
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
