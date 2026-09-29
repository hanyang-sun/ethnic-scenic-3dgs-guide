using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace CSU.Tour
{
    /// <summary>写到 persistentDataPath/Tours 的带版本 JSONL 日志。</summary>
    public sealed class TourLog : IDisposable
    {
        [Serializable]
        sealed class Settings
        {
            public string scene_revision = "garden_re0_tour_app";
            public string movement_mode = "planar_character_controller";
            public string model_name;
            public int splat_count;
            public int sh_order;
            public int width;
            public int height;
            public float speed;
            public float camera_fov;
            public float controller_radius;
            public Vector3 start_position;
            public float start_yaw;
            public Vector3 model_position;
            public Quaternion model_rotation;
            public Vector3 model_scale;
            public Bounds ground_bounds;
            public Vector3[] nodes;
            public TourEdge[] edges;
            public TourPoi[] pois;
        }

        [Serializable]
        sealed class Row
        {
            public int schema_version = 1;
            public int tour_index;
            public int route_revision;
            public string session_id;
            public string app_version;
            public string scene_id = "garden_re0";
            public string coordinate_system = "Unity world; Y up; scene units";
            public string utc;
            public string type;
            public string detail;
            public string poi_id;
            public string target_poi;
            public string route_id;
            public string route_status;
            public string scene_config_id;
            public double time;
            public Vector3 user_position;
            public Vector3 user_forward;
            public Vector3 camera_position;
            public Vector3 camera_forward;
            public Quaternion camera_rotation;
            public float route_length;
            public float remaining_distance;
            public float travel_distance;
            public float fps;
            public float frame_time_ms;
            public bool arrived;
            public bool paused;
            public bool bounds_overlay;
            public bool route_overlay;
            public Vector3[] route_points;
        }

        readonly TourApp app;
        readonly string configJson;
        readonly string configId;
        StreamWriter writer;

        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public string Path { get; private set; }
        public string Warning { get; private set; }

        public TourLog(TourApp owner)
        {
            app = owner;
            configJson = JsonUtility.ToJson(new Settings
            {
                model_name = app.splat.m_Asset.name,
                splat_count = app.splat.m_Asset.splatCount,
                sh_order = app.splat.m_SHOrder,
                width = Screen.width,
                height = Screen.height,
                speed = app.speed,
                camera_fov = app.view.fieldOfView,
                controller_radius = app.motor.radius,
                start_position = app.startPosition,
                start_yaw = app.startYaw,
                model_position = app.splat.transform.position,
                model_rotation = app.splat.transform.rotation,
                model_scale = app.splat.transform.localScale,
                ground_bounds = app.ground.bounds,
                nodes = app.nodes,
                edges = app.edges,
                pois = app.pois
            });
            configId = Hash128.Compute(configJson).ToString();

            try
            {
                string directory = System.IO.Path.Combine(Application.persistentDataPath, "Tours");
                Directory.CreateDirectory(directory);
                Path = System.IO.Path.Combine(directory,
                    DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + SessionId + ".jsonl");
                writer = new StreamWriter(Path, false, new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                Warning = "Logging unavailable: " + exception.Message;
            }
        }

        public void Write(string type, string detail, Transform pose, string poiId = "")
        {
            if (writer == null) return;
            try
            {
                var row = new Row
                {
                    session_id = SessionId,
                    app_version = Application.version,
                    tour_index = app.TourIndex,
                    utc = DateTime.UtcNow.ToString("O"),
                    time = Time.realtimeSinceStartupAsDouble,
                    type = type,
                    detail = detail,
                    poi_id = poiId,
                    user_position = app.player.position,
                    user_forward = app.player.forward,
                    camera_position = pose.position,
                    camera_forward = pose.forward,
                    camera_rotation = pose.rotation,
                    target_poi = app.Selected >= 0 ? app.pois[app.Selected].id : "",
                    route_id = app.RouteId,
                    route_revision = app.RouteRevision,
                    route_length = app.PlannedLength,
                    remaining_distance = app.RemainingDistance,
                    travel_distance = app.TravelDistance,
                    arrived = app.Arrived,
                    paused = app.paused,
                    frame_time_ms = app.FrameTimeMs,
                    bounds_overlay = app.showPoiBounds,
                    route_overlay = app.showRoute,
                    scene_config_id = configId,
                    route_status = app.Selected < 0 ? "none" :
                        app.Arrived ? "arrived" :
                        app.RouteRevision == 0 ? "pending" :
                        app.Route.Count > 0 ? "available" : "unreachable",
                    fps = app.FrameTimeMs > 0f ? 1000f / app.FrameTimeMs : 0f
                };

                if (type == "route_updated")
                {
                    row.route_points = new Vector3[app.Route.Count];
                    for (int i = 0; i < row.route_points.Length; i++)
                        row.route_points[i] = app.Route[i];
                }

                string json = JsonUtility.ToJson(row);
                if (type == "session_start")
                    json = json.Substring(0, json.Length - 1) + ",\"session_settings\":" + configJson + "}";

                writer.WriteLine(json.Substring(0, json.Length - 1) +
                    ",\"research\":{\"poi_visibility\":null,\"screen_coverage\":null," +
                    "\"viewing_time\":null,\"gaze_target\":null,\"path_quality\":null," +
                    "\"artifact_score\":null}}");
            }
            catch (Exception exception)
            {
                Warning = "Logging stopped: " + exception.Message;
                Dispose();
            }
        }

        public void Flush()
        {
            try { writer?.Flush(); }
            catch (Exception exception)
            {
                Warning = exception.Message;
                Dispose();
            }
        }

        public void Dispose()
        {
            StreamWriter current = writer;
            writer = null;
            try { current?.Dispose(); }
            catch (Exception) { }
        }
    }
}
