﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;


namespace FractalRenderTask
{
    [Serializable]
    public class Task
    {
        TaskData taskData;
        public class Mandelbox
        {
            private TaskData.MandelboxData data;
            public Mandelbox()
            {
                data = null;
            }
            public Mandelbox(TaskData.MandelboxData data)
            {
                this.data = data;
            }
            private void sphereFold(ref Vector3 z, ref float dz)
            {
                float r2 = Vector3.Dot(z + data.sphereFoldOffset, z + data.sphereFoldOffset);
                if (r2 < data.minRadius2)
                {
                    // linear inner scaling
                    float temp = (data.fixedRadius2 / data.minRadius2);
                    z *= temp;
                    dz *= temp;
                }
                else if (r2 < data.fixedRadius2)
                {
                    // this is the actual sphere inversion
                    float temp = (data.fixedRadius2 / r2);
                    z *= temp;
                    dz *= temp;
                }
            }
            void boxFold(ref Vector3 z, ref float dz)
            {
                if (Math.Abs(z.X) > data.foldingLimit)
                {
                    z.X = data.foldingValue * Math.Sign(z.X) - z.X;
                }
                if (Math.Abs(z.Y) > data.foldingLimit)
                {
                    z.Y = data.foldingValue * Math.Sign(z.Y) - z.Y;
                }
                if (Math.Abs(z.Z) > data.foldingLimit)
                {
                    z.Z = data.foldingValue * Math.Sign(z.Z) - z.Z;
                }
            }
            public float DE(Vector3 p)
            {
                float dr = 1.0f;
                Vector3 offset = p;
                int i;
                float r = 0.0f;
                Vector3 t = p;
                for (i = 0; i < data.iterations; i++)
                {
                    boxFold(ref p, ref dr);
                    sphereFold(ref p, ref dr);
                    p = data.scale * p + offset;
                    dr = dr * Math.Abs(data.scale) + 1.0f;
                    r = p.Length();
                    if (r * r > data.bailout)
                    {
                        float r2 = Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z)));
                        return r / Math.Abs(dr);
                    }
                }
                return r / Math.Abs(dr);
            }
        }
        public Task()
        {
        }
        public float traceBox(Vector3 ro, Vector3 rd,
                          Vector3 boxSize)
        {
            Vector3 m = new Vector3(1.0f/rd.X,1.0f/rd.Y,1.0f/rd.Z);
            Vector3 n = m * ro;
            Vector3 k = (new Vector3(Math.Abs(m.X),Math.Abs(m.Y),Math.Abs(m.Z))) * boxSize;

            Vector3 t1 = -n - k;
            Vector3 t2 = -n + k;

            float tN = Math.Max(Math.Max(t1.X, t1.Y), t1.Z);
            float tF = Math.Min(Math.Min(t2.X, t2.Y), t2.Z);

            if (tN > tF || tF < 0.0) return -1.0f; // no intersection

            return tN;
        }
        public float trace(Mandelbox mandelbox, TaskData data, Vector3 ro, Vector3 rd, ref Vector3 normal)
        {
            float t = 0.0f;
            t=traceBox(ro,rd,new Vector3(data.boxSize));
            float epsilon = data.epsilon * (t + 1.0f);
            for (int i = 0; i < data.rayIterations; i++)
            {
                Vector3 pos = ro + rd * t;
                //mapSphereToCube(pos_t);
                //minRadius2=0.25+(fBm(vec2(atan(pos.y,pos.x),acos(pos.z/r)))-0.5)*0.01;
                float dp = mandelbox.DE(pos);
                if (dp < epsilon)
                {
                    float ddp = dp * 0.75f;
                    //calc gradient
                    float dp1 = mandelbox.DE(pos + new Vector3(ddp, 0.0f, 0.0f));
                    normal.X = (dp1 - mandelbox.DE(pos - new Vector3(ddp, 0.0f, 0.0f)));
                    dp1 = mandelbox.DE(pos + new Vector3(0.0f, ddp, 0.0f));
                    normal.Y = (dp1 - mandelbox.DE(pos - new Vector3(0.0f, ddp, 0.0f)));
                    dp1 = mandelbox.DE(pos + new Vector3(0.0f, 0.0f, ddp));
                    normal.Z = (dp1 - mandelbox.DE(pos - new Vector3(0.0f, 0.0f, ddp)));
                    normal = Vector3.Normalize(-normal);
                    return t;
                }
                dp *= data.scaleStep;
                t += dp;
                epsilon += data.epsilon * (t + 1.0f);
            }
            return -1.0f;
        }
        public void getCamera(Vector2 uv, out Vector3 ro, out Vector3 rd, TaskData.Camera cam)
        {
            uv *= 2.0f;
            ro = cam.pos;
            Vector3 up = Vector3.Normalize(cam.up);
            rd = Vector3.Normalize(cam.dir);
            Vector3 right = Vector3.Cross(rd, up);
            up = Vector3.Cross(right, rd);
            rd = Vector3.Normalize(rd + (float)(Math.Tan(Math.PI / 180.0f * cam.fov / 2.0f)) * 2.0f * (right * uv.X + up * uv.Y));
        }
        public Vector3 getColor(Mandelbox mandelbox, TaskData data, int x, int y, int width, int height)
        {
             Vector2 uv = new Vector2((float)x / width - 0.5f, (float)y / width - (float)(height * 0.5) / width);
             Vector3 ro;
             Vector3 rd;
             getCamera(uv,out ro,out rd,data.camera);
             Vector3 normal = new Vector3(0.0f);
             float t = trace(mandelbox,data,ro,rd,ref normal);
             if (t >= 0.0)
             {
                 return new Vector3(Math.Max(0.0f,Vector3.Dot(rd,normal)));
             }
            return new Vector3(0.0f, 0.0f, 0.0f);
        }
        public byte[] execute(TaskData taskData, int workerNumber, int workersCount)
        {
            int pixels = taskData.width * taskData.height;
            //int tileSize = (int)Math.Ceiling((float)(pixels - workerNumber) / (float)workersCount);
            int sz = (int)Math.Ceiling((float)pixels / (float)workersCount);
            int index = (workerNumber) * sz;
            int tileSize = Math.Min(pixels - index, sz);
            byte[] pixelColors = new byte[3 * tileSize];
            Mandelbox mandelbox = new Mandelbox(taskData.mandelbox);
            for (int i = workerNumber,j=0; i < pixels&&j<tileSize; i += workersCount,j++)
            {
                Vector3 res = getColor(mandelbox, taskData, (index + j) % taskData.width, (index + j) / taskData.width, taskData.width, taskData.height);
                res *= 255.0f;
                res.X = Math.Min(Math.Max(res.X, 0.0f), 255.0f);
                res.Y = Math.Min(Math.Max(res.Y, 0.0f), 255.0f);
                res.Z = Math.Min(Math.Max(res.Z, 0.0f), 255.0f);
                pixelColors[j * 3] = (byte)(res.X);
                pixelColors[j * 3 + 1] = (byte)(res.Y);
                pixelColors[j * 3 + 2] = (byte)(res.Z);
            }
            return pixelColors;
        }
        public bool validate(string data)
        {
            taskData = new TaskData();
            try
            {
                taskData = JsonConvert.DeserializeObject<TaskData>(data);
            }
            catch (Exception exc)
            {
                throw exc;
            }
            return true;
        }
        public TaskData parseData(string serializedData)
        {
            taskData = new TaskData();
            try
            {
                taskData = JsonConvert.DeserializeObject<TaskData>(serializedData);
            }
            catch (Exception exc)
            {
                throw exc;
            }
            return taskData;
        }
        public void showResults(List<byte[]> dataArrays)//ascending order of workerNumbers
        {
            byte[] imageArray = dataArrays.SelectMany(byteArr => byteArr).ToArray();
            taskWindow window = new taskWindow(imageArray,taskData.width,taskData.height);
            window.ShowDialog();
        }
    }
    public class TaskData
    {
        public class MandelboxData
        {
            public float foldingLimit = 1.00f;
            public float minRadius2 = 0.2500f;
            public float scale = -1.500f;
            public float foldingValue = 2.0000000f;
            public Vector3 sphereFoldOffset = new Vector3(0.0f, 0.0f, 0.00f);
            public float fixedRadius2 = 1.000f;
            public int iterations = 30;
            public float bailout = 10240.0f;
            public MandelboxData()
            { 
            }
        }
        public class Camera
        {
            public Vector3 pos;
            public Vector3 dir;
            public Vector3 up;
            public float aperture;
            public float focalDist;
            public float fov;
            public Camera()
            { 
            }
        }
        public float epsilon = 0.001f;
        public float scaleStep = 0.9800f;
        public int rayIterations=60;
        public Camera camera;
        public MandelboxData mandelbox;
        public int width;
        public int height;
        public float boxSize;
    }
}
