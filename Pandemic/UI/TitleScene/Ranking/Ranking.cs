using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Main.UI.TitleScene.Ranking {
    public class Ranking : MonoBehaviour {

        public Text[] myInfos = new Text[5];

        public Item[] items;

        void Start() {
            StartCoroutine(GetRanking());
        }

        IEnumerator GetRanking() {
            var userSrl = 1;
            List<IMultipartFormSection> form = new List<IMultipartFormSection>();
            form.Add(new MultipartFormDataSection("user_srl", userSrl.ToString()));

            var webRequest = UnityWebRequest.Post("https://kmsjun2514.cafe24.com/1917/pandemic/get_ranking_all.php", form);
            yield return webRequest.SendWebRequest();

            var data = JsonUtility.FromJson<RankingData>(webRequest.downloadHandler.text);

            var playtime = data.my_info.playtime;
            var m = playtime / 60;
            var s = playtime % 60;

            myInfos[0].text = m + " : " + s;
            myInfos[1].text = data.my_info.help.ToString();
            myInfos[2].text = data.my_info.medkit.ToString();
            myInfos[3].text = data.my_info.headshot.ToString();
            myInfos[4].text = data.my_info.score.ToString();

            for (int i = 0; i < data.ranking.Count; i++) {
                var username = "";
                switch (data.ranking[i].user_srl) {
                    case 1: username = "junemay00"; break;
                    case 2: username = "rhwnsgud1"; break;
                    case 3: username = "uk4ang"; break;
                    case 4: username = "Kaywon2021"; break;
                    default: username = "Unknown"; break;
                }
                items[i].username.text = username;
                items[i].score.text = data.ranking[i].score.ToString();

                if (data.ranking[i].user_srl == userSrl) {
                    items[i].backgroundImage.sprite = items[i].grayBoxSprite;
                    items[i].rankBackgroundImage.sprite = items[i].yellowBoxSprite;
                }
            }
        }

        [System.Serializable]
        public class RankingData {
            public List<Data> ranking = new List<Data>();
            public Data my_info;

            [System.Serializable]
            public class Data {
                public int srl;
                public int user_srl;
                public int playtime;
                public int help;
                public int medkit;
                public int headshot;
                public int score;
            }
        }
    }
}
