using System;
using System.Text;
using System.Threading;
using System.Net.Sockets;

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Xoxxox;

namespace Xoxxox {
  public class RcvAct: MVRScript {

    // 変数を定義
    public string srvadr = Params.srvadr; // サーバのアドレス
    public int srvprt = Params.srvprt; // サーバのポート
    public string pthsnd = ParamsAct.pthsnd; // サーバのパス（送信用）
    public int secchk = 5000; // 検査を待つ時間（マイクロ秒）

    private Thread trdbak; // スレッド（バックグラウンド）
    private byte[] bytcon; // 内容アレイ
    private bool flgrcv = false; // 検査フラグ（内容を受信する可否）
    private JSONStorableString strsnd; // 入力欄（サーバのパス（送信用））

    // 処理を開始
    private void Start() {
      // 作成：ＵＩ：入力欄（サーバのパス（送信用））
      strsnd = new JSONStorableString("pthsnd", pthsnd);
      RegisterString(strsnd);
      var t01 = CreateTextField(strsnd);
      t01.height = 0.25f;
      var i01 = t01.gameObject.AddComponent<InputField>();
      i01.textComponent = t01.UItext;
      strsnd.inputField = i01;
      // 作成：スレッド
      trdbak = new Thread(ExeBak); // データ送信（スレッド：バックグラウンド））
      trdbak.Start();
    }

    // 処理を反復（フォアグラウンド）
    private void Update() {
      if (flgrcv == true) { // 検査フラグが真（内容を受信可能）なら
        try {
          string keybtn = Encoding.UTF8.GetString(bytcon);
          SuperController.LogMessage("RcvAct:sts[" + keybtn + "]"); // DBG
          Atom atmbtn = SuperController.singleton.GetAtomByUid(keybtn);
          Transform trnbtn = atmbtn.reParentObject.Find("object/rescaleObject/Canvas/Button");
          trnbtn.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }
        catch (Exception e) {
          SuperController.LogError("RcvAct:err[" + e + "]"); // DBG
        }
        flgrcv = false; // 検査フラグを偽
      }
    }

    // 処理を反復（バックグラウンド）
    private void ExeBak() {
      while (true) {
        SuperController.LogMessage("RcvAct:bgn[]"); // DBG
        PolAct();
        SuperController.LogMessage("RcvAct:end[]"); // DBG
        flgrcv = true; // 検査フラグを真（内容が受信可能）
        Thread.Sleep(secchk); // 指定時間だけ待機
      }
    }

    // 内容を受信
    private void PolAct() {
      pthsnd = strsnd.val;
      string strreq =
          $"POST {pthsnd} HTTP/1.1\r\n" +
          "Host: {srvadr}\r\n" +
          "Connection: Close\r\n\r\n";
      byte[] bytreq = Encoding.ASCII.GetBytes(strreq);

      using (TcpClient client = new TcpClient(srvadr, srvprt))
      using (NetworkStream stream = client.GetStream()) {
        stream.Write(bytreq, 0, bytreq.Length); // リクエストを送信（HTTP）
        byte[] arrbuf = new byte[2048];
        int posbuf;
        bool flghed = false;
        StringBuilder bldhed = new StringBuilder();
        bytcon = new byte[0];

        while ((posbuf = stream.Read(arrbuf, 0, arrbuf.Length)) > 0) {
          if (!flghed) {
            bldhed.Append(Encoding.ASCII.GetString(arrbuf, 0, posbuf));
            string strtmp = bldhed.ToString();
            int possep = strtmp.IndexOf("\r\n\r\n"); // ヘッダとボディの境界を検出
            if (possep >= 0) {
              flghed = true;
              int lenhed = Encoding.ASCII.GetByteCount(strtmp.Substring(0, possep + 4));
              bytcon = new byte[posbuf - lenhed];
              Array.Copy(arrbuf, lenhed, bytcon, 0, posbuf - lenhed);  // ヘッダを除いた部分を保存
            }
          }
          else { // すでに読み込んだ内容データに、新しく読み込んだ内容データを追加
            int lencon = bytcon.Length;
            Array.Resize(ref bytcon, lencon + posbuf);
            Array.Copy(arrbuf, 0, bytcon, lencon, posbuf);
          }
        }
      }
    }

    // 処理を終了
    private void OnDestroy() {
      if (trdbak != null && trdbak.IsAlive) {
        trdbak.Abort();
      }
    }
  }
}
