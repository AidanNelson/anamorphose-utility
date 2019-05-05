// Anamorphose Utility
// Spring, 2019 - Aidan Nelson
// Based on: https://www.lifl.fr/~decomite/anamorphoses/tutorial/tutorial.html
// References:
// https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/refract.xhtml


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnamorphoseMesh : MonoBehaviour
{

    [Header("Public Game Objects")]
    public Camera meshCam;
    public GameObject outputMeshObject;
    public GameObject lens;
    public GameObject target;




    [Header("Settings")]
    public bool liveMode = true;
    public bool showRays = true;
    public bool ShowGridMarkers = true;
    [Range(1f, 10f)]
    public float gridMarkerSize = 1f;
    [Range(1f, 5000f)]
    public float meshDistThreshold = 100f;



    [Header("Indices of Refraction")]
    // indices of refraction
    public float n1 = 1.0f;
    [Range(1f, 1.8f)]
    public float n2 = 1.4f;

    [Header("Positions (in mm)")]
    [Range(-1f, -1000f)]
    public float eyePos = -350f;
    [Range(-1f, -1000f)]
    public float virtualScreenPos = -10f;
    [Range(1f, 5000f)]
    public float targetPos = 500f;


    [Header("Virtual Image Size (in mm)")]
    [Range(1f, 200f)]
    public float hSize = 60f;
    [Range(1f, 200f)]
    public float vSize = 60f;

    [Header("Target Screen Size")]
    [Range(100f, 5000f)]
    public int screenWidth = 300;
    [Range(100f, 5000f)]
    public int screenHeight = 200;


    [Header("Resolution of Mesh")]
    [Range(1, 1000f)]
    public int userSetNumCols = 20;
    [Range(1, 1000f)]
    public int userSetNumRows = 20;

    private int numCols;
    private int numRows;


    [Header("Width of Debugging Lines")]
    [Range(0.1f, 5f)]
    public float lineWidth = 1f;

    [Header("Offsets")]
    [Range(0.0f, 1f)]
    public float offsetPercentage = 0.0f;
    [Range(0f, 10f)]
    public float secondRayOffset = 5f;




    // private variables
    private Vector3 eyePt;
    private Vector3[] screenPoints; // 'virtual image' points
    private Vector3[] refractedPoints; 
    private Mesh mesh;
    private bool meshCreated = false;

    // these will store markers and lines gameobjects for debugging purposes
    private GameObject lineContainer;
    private GameObject markerContainer;



    void Start()
    {
        // Game Objects for storing line and marker containers:
        lineContainer = new GameObject("lineContainer");
        markerContainer = new GameObject("markerContainer");

        // Set initial positions
        SetPositions();

        // Set numCols/numRows
        numCols = userSetNumCols;
        numRows = userSetNumRows;

        // then create a grid of points
        screenPoints = GetScreenPoints(hSize, numCols, vSize, numRows);

        // then cast rays through those points and get the grid of refracted points
        refractedPoints = CastRays(screenPoints, eyePt);

        // finally draw that grid
        MakeGridMarkers();
    }

    // this update function can be called from other scripts to allow easy integration into
    // a interactive system
    public void UpdateOnce()
    {

        SetPositions();

        numCols = userSetNumCols;
        numRows = userSetNumRows;

        screenPoints = GetScreenPoints(hSize, numCols, vSize, numRows);
        refractedPoints = CastRays(screenPoints, eyePt);

        target.GetComponent<MeshRenderer>().enabled = false;
        mesh = GenerateMesh(refractedPoints);
        meshCreated = true;
    }

    void Update()
    {

        // if we are in liveMode, reset everything every frame
        if (liveMode)
        {
            SetPositions();

            // make sure we aren't going overboard with the grid resolution in live mode
            numCols = userSetNumCols > 40 ? 40 : userSetNumCols;
            numRows = userSetNumRows > 40 ? 40 : userSetNumRows;

            screenPoints = GetScreenPoints(hSize, numCols, vSize, numRows);

            RemoveLines();
            refractedPoints = CastRays(screenPoints, eyePt);

            MakeGridMarkers();
            mesh = GenerateMesh(refractedPoints);

            meshCreated = false;
        }
        // otherwise, create the mesh (once)
        else if (!meshCreated)
        {
            numCols = userSetNumCols;
            numRows = userSetNumRows;

            screenPoints = GetScreenPoints(hSize, numCols, vSize, numRows);
            refractedPoints = CastRays(screenPoints, eyePt);

            target.GetComponent<MeshRenderer>().enabled = false;
            mesh = GenerateMesh(refractedPoints);

            // set meshCreated to true so we know not to create it again next frame
            meshCreated = true;
        }
    }





    // this function sets the position of various objects in the scene according to the user input
    void SetPositions()
    {
        screenPoints = GetScreenPoints(hSize, numCols, vSize, numRows);

        // target
        // this buffer makes the target bigger than the desired screen
        // content outside of the screen area just won't be rendered
        int buffer = 2000; 
        target.transform.position = new Vector3(0.0f, 0.0f, targetPos);
        target.transform.localScale = new Vector3(screenWidth / 10 + buffer, 1f, screenHeight / 10 + buffer);

        // eye position
        eyePt = new Vector3(0.0f, 0.0f, eyePos);

        // lens
        lens.transform.position = new Vector3(0.0f, 0.0f, 0.0f);
        lens.transform.rotation = Quaternion.identity;
        lens.transform.rotation = Quaternion.AngleAxis(-90, Vector3.right);

        // meshCam
        meshCam.transform.position = new Vector3(0, 0, targetPos - 10f);
        meshCam.transform.rotation = Quaternion.identity;

        float aspectRatio = Mathf.Round(screenWidth) / Mathf.Round(screenHeight);
        meshCam.aspect = aspectRatio;
        meshCam.orthographicSize = screenHeight / 2;
    }




    // for every point in the screenpoint array, this casts a ray through it and
    // two layers of the lens    
    Vector3[] CastRays(Vector3[] _screenPoints, Vector3 _eyePoint)
    {
        // http://wiki.unity3d.com/index.php/Choosing_the_right_collection_type
        Vector3[] _refractedGrid = new Vector3[_screenPoints.Length];

        // iterate thorugh our array of grid points:
        for (int i = 0; i < _screenPoints.Length; i++)
        {
            Vector3 pt = _screenPoints[i];
            Ray ray = new Ray(_eyePoint, pt - _eyePoint);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Vector3 incidentVec = hit.point - _eyePoint;

                // Use the point's normal to calculate the reflection vector.
                Vector3 refractVec = RefractVector(incidentVec, hit.normal, n1, n2);
                if (refractVec == new Vector3(0.0f, 0.0f, 0.0f))
                {
                    Debug.Log("Bad 1st refraction!");
                }

                // Draw lines to show the incoming "beam" and the refraction.
                if (showRays)
                {
                    Debug.DrawLine(_eyePoint, hit.point, Color.red);
                    AddLine(_eyePoint, hit.point, Color.red, lineWidth);

                }


                // https://www.reddit.com/r/Unity3D/comments/7k9wwi/laser_refraction_problem/
                // "It seems the comment on my post that I followed has disappeared, but the general gist is to take a point arbitrarily along the laser's ray, then do a raycast in the opposite direction specifically for the collider that the laser has entered. ([collider].Raycast()) This should get you the point where the ray exits the collider. Using this point, you should be able to use the same refraction maths, but with most of the vectors inverted, and the indices of refraction flipped around, because the ray is now exiting the medium. Hope this helps!"
                // assumes lenses less that 25 units 
                Bounds lensBounds = lens.GetComponentInChildren<Renderer>().bounds;

                float offset = lensBounds.size.z + secondRayOffset;
                Vector3 pointAlongRefracted = hit.point + (refractVec.normalized * offset);
                Ray backwardsRay = new Ray(pointAlongRefracted, -1f * refractVec);

                RaycastHit hitTwo;

                if (Physics.Raycast(backwardsRay, out hitTwo))
                {
                    // Find the line from the gun to the point that was clicked.
                    Vector3 incidentVecTwo = hitTwo.point - hit.point;

                    Vector3 refractVecTwo = RefractVector(incidentVecTwo, -1f * hitTwo.normal, n2, n1);

                    if (refractVecTwo == new Vector3(0.0f, 0.0f, 0.0f))
                    {
                        Debug.Log("Bad 2nd refraction!");
                    }

                    if (showRays)
                    {
                        AddLine(hit.point, hitTwo.point, Color.blue, lineWidth);
                    }

                    // we offset the hitpoint slightly
                    Vector3 offsetHitPointTwo = hitTwo.point + (refractVecTwo.normalized * offsetPercentage);

                    Ray rayThree = new Ray(offsetHitPointTwo, refractVecTwo);
                    RaycastHit hitThree;


                    if (Physics.Raycast(rayThree, out hitThree))
                    {
                        _refractedGrid[i] = hitThree.point;
                        if (showRays)
                        {
                            AddLine(hitTwo.point, hitThree.point, Color.yellow, lineWidth);
                        }
                    }
                    else
                    {
                        if (showRays)
                        {
                            Debug.DrawRay(hitTwo.point, rayThree.direction * 50f, Color.yellow);
                        }
                        Debug.LogWarning("2nd refraction misses target!");
                        _refractedGrid[i] = new Vector3(-100f, -100f, -100f);
                    }
                }
                else
                {
                    Debug.LogWarning("1st refraction misses target!");
                }
            }
            else
            {
                Debug.LogWarning("Ray from eyePoint misses target!");
            }
        }
        return _refractedGrid;
    }




    // this function should return an array of vec3s representing points on a screen at a certain
    // size, location and orientation 
    // 1:1 aspect ratio
    // grid extremes -1,-1, 1, 1

    // bottom left point --> bottom right point then up by row
    Vector3[] GetScreenPoints(float hSize, int numCols, float vSize, int numRows)
    {
        Vector3[] points = new Vector3[numCols * numRows];
        Vector3 bottomLeftPoint = new Vector3(-hSize / 2, -vSize / 2, virtualScreenPos);

        float vStep = vSize / (numRows - 1);
        float hStep = hSize / (numCols - 1);
        if (numRows == 1)
        {
            vStep = 0;
            bottomLeftPoint = new Vector3(-hSize / 2, 0f, virtualScreenPos);
        }
        if (numCols == 1)
        {
            hStep = 0;
            bottomLeftPoint = new Vector3(0f, -vSize / 2, virtualScreenPos);
        }
        if (numCols == 0 && numRows == 0)
        {
            bottomLeftPoint = new Vector3(0f, 0f, virtualScreenPos);
        }

        // move bottom left to top right:
        // iterate through rows (Y)
        for (int i = 0; i < numRows; i++)
        {
            // iterate through columns (X)
            for (int j = 0; j < numCols; j++)
            {
                float xVal = j * hStep;
                float yVal = i * vStep;

                Vector3 pt = new Vector3(xVal, yVal, 0);

                int arrIndex = (i * numCols) + j;
                points[arrIndex] = pt + bottomLeftPoint;
            }
        }
        return points;
    }



    // based on the following implementation of vector refraction
    // https://github.com/marczych/RayTracer/blob/master/src/RayTracer.cpp

    Vector3 RefractVector(Vector3 incident, Vector3 normal, float n1, float n2)
    {
        float n = n1 / n2;
        Vector3 incidentNormalized = Vector3.Normalize(incident);
        Vector3 normalNormalized = Vector3.Normalize(normal);

        float cosI = -1.0f * Vector3.Dot(normalNormalized, incidentNormalized);
        float sinT2 = (n * n) * (1.0f - (cosI * cosI));

        if (sinT2 > 1.0f)
        {
            Debug.LogError("Bad refraction Vector!");
        }

        float cosT = Mathf.Sqrt(1.0f - sinT2);

        float amnt = (n * cosI - cosT);
        Vector3 newNormal = normalNormalized * amnt;

        return ((incidentNormalized * n) + newNormal);
    }



    Mesh GenerateMesh(Vector3[] meshPoints)
    {
        Mesh mesh = new Mesh();
        outputMeshObject.GetComponent<MeshFilter>().mesh = mesh;

        // Vector3[] verticies = (Vector3[]) meshPoints.Clone();
        Vector2[] uvs = new Vector2[meshPoints.Length];
        List<int> triangleIndices = new List<int>();

        // scalar for UV coords
        float uvStepVert = 1f / (numRows - 1);
        float uvStepHorz = 1f / (numCols - 1);

        // iterate through rows (Y), ending one row away from the top
        for (int i = 0; i < numRows - 1; i++)
        {
            // iterate through columns (X), ending one column away from the right
            for (int j = 0; j < numCols - 1; j++)
            {

                // triangle indices
                int tri1 = (i + 0) * (numCols) + (j + 0);//0
                int tri2 = (i + 1) * (numCols) + (j + 0);//4
                int tri3 = (i + 0) * (numCols) + (j + 1);//1
                int tri4 = (i + 1) * (numCols) + (j + 0);//4
                int tri5 = (i + 1) * (numCols) + (j + 1);//5
                int tri6 = (i + 0) * (numCols) + (j + 1);//1

                // get vertices to check orientation
                Vector3 vert0 = meshPoints[tri1];
                Vector3 vert1 = meshPoints[tri3];
                Vector3 vert4 = meshPoints[tri4];
                Vector3 vert5 = meshPoints[tri5];

                if (Vector3.Distance(vert0, vert1) < meshDistThreshold)
                {
                    triangleIndices.Add(tri1);
                    triangleIndices.Add(tri2);
                    triangleIndices.Add(tri3);
                }
                else
                {
                    Debug.Log("Mesh triangle too big! Skipping!");
                }

                if (Vector3.Distance(vert5, vert4) < meshDistThreshold)
                {
                    triangleIndices.Add(tri4);
                    triangleIndices.Add(tri5);
                    triangleIndices.Add(tri6);
                }
                else
                {
                    Debug.Log("Mesh triangle too big! Skipping!");
                }

            }
        }

        // iterate through rows (Y)
        for (int i = 0; i < numRows; i++)
        {
            // iterate through columns (X)
            for (int j = 0; j < numCols; j++)
            {
                int gridIndex = i * (numCols) + j;
                uvs[gridIndex] = new Vector2(j * uvStepHorz, i * uvStepVert);
            }
        }


        mesh.vertices = meshPoints;
        mesh.uv = uvs;
        mesh.triangles = triangleIndices.ToArray();

        return mesh;
    }



    ///////////////////////////////////////////////////////////////////////////
    // Helper & Display Functions
    ///////////////////////////////////////////////////////////////////////////


    // MakeGridMarkers() instantiates spheres at points on the virtual screen and refracted grid
    // as a debugging measure
    void MakeGridMarkers()
    {

        foreach (Transform child in markerContainer.transform)
        {//Clear brushes
            Destroy(child.gameObject);
        }
        // then, if we are in liveMode, instantiate them again
        if (ShowGridMarkers)
        {
            for (int i = 0; i < screenPoints.Length; i++)
            {
                GameObject screenPointMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                screenPointMarker.transform.position = screenPoints[i];
                screenPointMarker.GetComponent<Renderer>().material.color = Color.black;
                screenPointMarker.GetComponent<Collider>().enabled = false;
                screenPointMarker.transform.localScale = new Vector3(gridMarkerSize, gridMarkerSize, gridMarkerSize);
                screenPointMarker.transform.parent = markerContainer.transform;
            }
            for (int i = 0; i < refractedPoints.Length; i++)
            {
                GameObject refractedGridMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                refractedGridMarker.transform.position = refractedPoints[i];
                refractedGridMarker.GetComponent<Renderer>().material.color = Color.black;
                refractedGridMarker.GetComponent<Collider>().enabled = false;
                refractedGridMarker.transform.localScale = new Vector3(gridMarkerSize, gridMarkerSize, gridMarkerSize); ;
                refractedGridMarker.transform.parent = markerContainer.transform;
            }

            // finally, add a marker for the eye
            GameObject eyeMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eyeMarker.transform.position = eyePt;
            eyeMarker.GetComponent<Renderer>().material.color = Color.black;
            eyeMarker.GetComponent<Collider>().enabled = false;
            eyeMarker.transform.localScale = new Vector3(gridMarkerSize, gridMarkerSize, gridMarkerSize); ;

            eyeMarker.transform.parent = markerContainer.transform;
        }
    }


    // to display lines in game view, we have the following three utilities 
    // remove lines removes all lines
    void RemoveLines()
    {
        foreach (Transform child in lineContainer.transform)
        {
            Destroy(child.gameObject);
        }
    }

    // AddLine() creates a line and adds it to the lineContainer GameObject
    void AddLine(Vector3 p1, Vector3 p2, Color col, float width)
    {
        GameObject line = CreateLine(p1, p2, col, width);
        line.transform.parent = lineContainer.transform;
    }

    // CreateLine() creates a GameObject with a LineRenderer Component
    GameObject CreateLine(Vector3 p1, Vector3 p2, Color col, float width)
    {
        GameObject myLineObj = new GameObject();
        LineRenderer lineRenderer = myLineObj.AddComponent<LineRenderer>();

        lineRenderer.widthMultiplier = width;

        var points = new Vector3[2];
        points[0] = p1;
        points[1] = p2;

        lineRenderer.SetPositions(points);

        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = col;

        return myLineObj;
    }
}