using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TwitchChannelPoints
{
    public partial class frmMain : Form
    {
        private BindingSource dataGridSource = new BindingSource();
        private string userId;
        private Dictionary<string, int> oldPoints = new Dictionary<string, int>();
        private Dictionary<string, int> broadcastCount = new Dictionary<string, int>();
        private int tickCount = 0;
        public frmMain()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.DataSource = dataGridSource;
            dataGridView1.AutoGenerateColumns = false;
            textAuth.Text = Properties.Settings.Default.AuthToken;

            List<string> streamerList = new List<string>(Properties.Settings.Default.Streamers.Split(','));
            foreach (var streamer in streamerList)
            {
                if (streamer.Trim() != "")
                    dataGridSource.Add(new Streamer() { Name = streamer });
            }

            timerMain_Tick(null, null);
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView1.CurrentCell != null)
            {
                btnDown.Enabled = true;
                btnUp.Enabled = true;
            }
            else
            {
                btnDown.Enabled = false;
                btnUp.Enabled = false;
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            string streamerName = Prompt.ShowDialog("Enter streamer name").Trim();
            if (streamerName != "")
            {
                dataGridSource.Add(new Streamer() { Name = streamerName });
            }
        }

        private void dataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            ChangePriorities();
        }

        private void dataGridView1_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            ChangePriorities();
        }

        private void ChangePriorities()
        {
            for (int i = 0; i < dataGridSource.Count; i++)
            {
                ((Streamer)dataGridSource[i]).Priority = (i + 1);
            }
            SaveStreamers();
        }

        private void SaveStreamers()
        {
            string streamerList = String.Join(",", dataGridSource.List.OfType<Streamer>().Select(x => x.Name));
            Properties.Settings.Default.Streamers = streamerList;
            Properties.Settings.Default.Save();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex] is DataGridViewButtonColumn && e.RowIndex >= 0)
            {
                dataGridSource.RemoveAt(e.RowIndex);
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            int index = dataGridView1.CurrentCell.RowIndex;
            if (dataGridSource.Count > index + 1)
            {
                Streamer streamer = (Streamer)dataGridSource[index];
                dataGridSource[index] = dataGridSource[index + 1];
                dataGridSource[index + 1] = streamer;
                dataGridView1.CurrentCell.Selected = false;
                dataGridView1.Rows[index+1].Cells[0].Selected = true;
                dataGridView1.CurrentCell = dataGridView1.Rows[index + 1].Cells[0];
                ChangePriorities();
            }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            int index = dataGridView1.CurrentCell.RowIndex;
            if (index != 0)
            {
                Streamer streamer = (Streamer)dataGridSource[index];
                dataGridSource[index] = dataGridSource[index - 1];
                dataGridSource[index - 1] = streamer;
                dataGridView1.CurrentCell.Selected = false;
                dataGridView1.Rows[index - 1].Cells[0].Selected = true;
                dataGridView1.CurrentCell = dataGridView1.Rows[index - 1].Cells[0];
                ChangePriorities();
            }
        }

        private void Save_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.AuthToken = textAuth.Text;
            Properties.Settings.Default.Save();
            userId = "";
        }

        private async void timerMain_Tick(object sender, EventArgs e)
        {
            try
            {
                string auth = Properties.Settings.Default.AuthToken.Trim();
                using WebClient clientTwitch = new WebClient();
                clientTwitch.Headers.Add("Authorization", "OAuth " + auth);
                clientTwitch.Headers.Add("Client-Id", "kimne78kx3ncx6brgo4mv6wki5h1ko");

                for (int i = 0; i < dataGridSource.Count; i++)
                {
                    if (((Streamer)dataGridSource[i]).Points == 0)
                    {
                        string gqlRes = await clientTwitch.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"operationName\": \"ChannelPointsContext\",\"variables\": {\"channelLogin\": \"" + ((Streamer)dataGridSource[i]).Name.ToLower() + "\"},\"extensions\": {\"persistedQuery\": {\"version\": 1, \"sha256Hash\": \"9988086babc615a918a1e9a722ff41d98847acac822645209ac7379eecb27152\"}}}");
                        JObject res = JObject.Parse(gqlRes);
                        int newPoints = res["data"]["community"]["channel"]["self"]["communityPoints"]["balance"].ToObject<int>();
                        ((Streamer)dataGridSource[i]).Points = newPoints;
                        dataGridView1.Refresh();
                    }
                }

                if (auth != "")
                {
                    if (userId == null)
                    {
                        string userRes = await clientTwitch.UploadStringTaskAsync("https://gql.twitch.tv/gql", "[{\"extensions\":{\"persistedQuery\":{\"version\":1,\"sha256Hash\":\"482be6fdcd0ff8e6a55192210e2ec6db8a67392f206021e81abe0347fc727ebe\"}},\"operationName\":\"Core_Services_Spade_CurrentUser\",\"variables\":{}}]");
                        JArray userObj = JArray.Parse(userRes);
                        userId = userObj[0]["data"]["currentUser"]["id"].ToString();
                    }
                    JObject liveRes = JObject.Parse(await clientTwitch.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"query\":\"query {users(logins: [" + String.Join(',', dataGridSource.List.OfType<Streamer>().Select(x => "\\\"" + x.Name + "\\\"")) + "]) {id,login,displayName,stream{id}}}\"}"));
                    List<Streamer> streamerList = new List<Streamer>();

                    for (int i = 0; i < liveRes["data"]["users"].Count(); i++)
                    {
                        if (liveRes["data"]["users"][i]["stream"].Type == JTokenType.Null)
                        {
                            liveRes["data"]["users"][i].Remove();
                            i--;
                        }
                    }

                    foreach (var streamData in liveRes["data"]["users"])
                    {
                        if (streamerList.Count == 2)
                            break;

                        if (streamData["stream"].Type == JTokenType.Null)
                            continue;

                        string broadcastId = streamData["stream"]["id"].ToString();
                        if (!broadcastCount.ContainsKey(broadcastId) || broadcastCount[broadcastId] < 6)
                        {
                            streamerList.Add(dataGridSource.List.OfType<Streamer>().Where(x => x.Name.ToLower() == streamData["login"].ToString().ToLower()).First());
                        }
                    }

                    if (tickCount++ % 60 == 0)
                    {
                        for (int i = 0; i < dataGridSource.Count; i++)
                        {
                            ((Streamer)dataGridSource[i]).SpadeUrl = null;
                        }
                    }

                    for (int i = 0; i < dataGridSource.Count; i++)
                    {
                        if (streamerList.Count == 2)
                            break;

                        if (liveRes["data"]["users"].Any(x => x["login"].ToString().ToLower() == ((Streamer)dataGridSource[i]).Name.ToLower()))
                        {
                            streamerList.Add((Streamer)dataGridSource[i]);
                        }
                    }

                    for (int i = 0; i < streamerList.Count; i++)
                    {
                        JToken streamData = liveRes["data"]["users"].Where(x => x["login"].ToString().ToLower() == streamerList[i].Name.ToLower()).First();
                        string gqlRes = await clientTwitch.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"operationName\": \"ChannelPointsContext\",\"variables\": {\"channelLogin\": \"" + streamerList[i].Name.ToLower() + "\"},\"extensions\": {\"persistedQuery\": {\"version\": 1, \"sha256Hash\": \"9988086babc615a918a1e9a722ff41d98847acac822645209ac7379eecb27152\"}}}");
                        JObject res = JObject.Parse(gqlRes);
                        int newPoints = res["data"]["community"]["channel"]["self"]["communityPoints"]["balance"].ToObject<int>();
                        streamerList[i].Points = newPoints;
                        if (res["data"]["community"]["channel"]["self"]["communityPoints"]["availableClaim"].ToString() != "")
                        {
                            string claim_id = res["data"]["community"]["channel"]["self"]["communityPoints"]["availableClaim"]["id"].ToString();
                            await clientTwitch.UploadStringTaskAsync("https://gql.twitch.tv/gql", "{\"operationName\": \"ClaimCommunityPoints\",\"variables\": {\"input\": {\"channelID\": \"" + streamData["id"].ToString() + "\", \"claimID\": \"" + claim_id + "\"}},\"extensions\": {\"persistedQuery\": {\"version\": 1, \"sha256Hash\": \"46aaeebe02c99afdf4fc97c7c0cba964124bf6b0af229395f1f6d1feed05b3d0\"}}}");
                        }

                        if (streamerList[i].SpadeUrl == null)
                        {
                            Regex rg = new Regex("(https://static.twitchcdn.net/config/settings.*?js)");
                            Match settingsRegex = rg.Match(await clientTwitch.DownloadStringTaskAsync("https://www.twitch.tv/" + streamerList[i].Name.ToLower()));
                            streamerList[i].SpadeUrl = JObject.Parse((await clientTwitch.DownloadStringTaskAsync(settingsRegex.Value)).Substring(28))["spade_url"].ToString();
                        }

                        JObject data = new JObject();
                        data["channel_id"] = streamData["id"].ToString();
                        data["broadcast_id"] = streamData["stream"]["id"].ToString();
                        data["player"] = "site";
                        data["user_id"] = userId;
                        JObject data_root = new JObject();
                        data_root["event"] = "minute-watched";
                        data_root["properties"] = data;
                        string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(data_root.ToString(Newtonsoft.Json.Formatting.None)));
                        await clientTwitch.UploadStringTaskAsync(streamerList[i].SpadeUrl, payload);

                        if (broadcastCount.ContainsKey(streamData["stream"]["id"].ToString()))
                        {
                            broadcastCount[streamData["stream"]["id"].ToString()] += 1;
                        }
                        else
                        {
                            broadcastCount[streamData["stream"]["id"].ToString()] = 1;
                        }

                        if (oldPoints.ContainsKey(streamerList[i].Name))
                        {
                            if (newPoints > oldPoints[streamerList[i].Name])
                            {
                                textLog.Text += "Recieved " + (newPoints - oldPoints[streamerList[i].Name]) + " points from channel " + streamerList[i].Name + "\n";
                                oldPoints[streamerList[i].Name] = newPoints;
                            }
                        }
                        else
                        {
                            oldPoints[streamerList[i].Name] = newPoints;
                        }
                    }

                    for (int j = 0; j < streamerList.Count; j++)
                    {
                        for (int i = 0; i < dataGridSource.Count; i++)
                        {
                            if (((Streamer)dataGridSource[i]).Name.ToLower() == streamerList[j].Name.ToLower())
                            {
                                dataGridSource[i] = streamerList[j];
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {

            }
            catch (Exception ex)
            {
                textLog.Text += ex + "\n";
            }
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }
    }

    //Modified from https://stackoverflow.com/a/5427121/12204538
    public static class Prompt
    {
        public static string ShowDialog(string caption)
        {
            Form prompt = new Form()
            {
                Width = 300,
                Height = 100,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            TextBox textBox = new TextBox() { Left = 20, Top = 10, Width = 240 };
            Button confirmation = new Button() { Text = "Ok", Left = 210, Width = 50, Top = 35, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }

    public class Streamer
    {
        public int Priority { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public int Points { get; set; }
        public string Remove { get; set; }
        public string SpadeUrl { get; set; }

        public Streamer()
        {
            Remove = "X";
        }
    }
}
