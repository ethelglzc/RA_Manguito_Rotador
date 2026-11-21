using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

public class MenuInicialGenerador : MonoBehaviour
{
    [System.Serializable]
    public class OpcionEjercicio
    {
        public string tituloEjercicio;
        [TextArea(1, 2)]
        public string descripcionCorta;
        [Tooltip("Nombre exacto de la escena en Build Settings")]
        public string nombreEscena;
    }

    [Header("Configuracion de Escenas / Lista de Ejercicios")]
    public List<OpcionEjercicio> listaEjercicios = new List<OpcionEjercicio>()
    {
        new OpcionEjercicio
        {
            tituloEjercicio = "1. Rotación Externa de Hombro",
            descripcionCorta = "Movimiento del antebrazo hacia afuera con codo a 90°",
            nombreEscena = "proto_1"
        },
        new OpcionEjercicio
        {
            tituloEjercicio = "2. Rotación Interna de Hombro",
            descripcionCorta = "Movimiento del antebrazo hacia el abdomen con codo a 90°",
            nombreEscena = "proto_2"
        }
    };

    [Header("Seguimiento Espacial y Comportamiento")]
    public Transform camaraUsuario;
    public float distanciaAlUsuario = 1.35f;
    public float velocidadSeguimiento = 4.5f;
    public bool permitirAgarrarPanel = true;
    [Tooltip("Activo: sigue suavemente tu mirada. Inactivo: permanece estatico frente a ti.")]
    public bool seguirMiradaUsuario = true;

    [Tooltip("Desplazamiento vertical en metros respecto a la altura de tus ojos (negativo = más abajo)")]
    public float desplazamientoVertical = -0.18f;

    [Header("Tipografia Personalizada")]
    public TMP_FontAsset fuenteUI;

    // Paleta identica a UIRehabilitacionGenerador
    private readonly Color colFondoPrincipal = new Color(0.01f, 0.12f, 0.37f, 0.94f);
    private readonly Color colTarjetaSecundaria = new Color(0.06f, 0.18f, 0.46f, 0.96f);
    private readonly Color colAcentoPrimario = new Color(0.14f, 0.44f, 0.91f, 1f);
    private readonly Color colBotonEjercicio = new Color(0.11f, 0.24f, 0.54f, 1f);
    private readonly Color colBotonAjuste = new Color(0.17f, 0.29f, 0.55f, 1f);
    private readonly Color colBotonConfirmar = new Color(0.07f, 0.56f, 0.38f, 1f);
    private readonly Color colBotonSalir = new Color(0.84f, 0.06f, 0.09f, 1f);
    private readonly Color colTextoClaro = new Color(1f, 1f, 1f, 1f);
    private readonly Color colTextoSuave = new Color(0.84f, 0.86f, 0.90f, 1f);
    private readonly Color colDestacadoVerde = new Color(0.18f, 0.90f, 0.62f, 1f);

    private GameObject canvasRaiz;
    private TextMeshProUGUI txtEstadoCarga;
    private TextMeshProUGUI txtBtnSeguimiento;
    private TextMeshProUGUI txtBtnTextoGrande;

    private bool siendoAgarrado = false;
    private bool modoTextoGrande = false;

    private class TextoEscalable
    {
        public TextMeshProUGUI tmp;
        public float tamanoBase;
    }

    private class RectEscalable
    {
        public RectTransform rt;
        public Vector2 tamanoBase;
        public Vector2 posBase;
    }

    private readonly List<TextoEscalable> textosRegistrados = new List<TextoEscalable>();
    private readonly List<RectEscalable> rectsRegistrados = new List<RectEscalable>();

    private void Awake()
    {
        if (camaraUsuario == null && Camera.main != null)
            camaraUsuario = Camera.main.transform;

        ConstruirMenuInicial();
    }

    private void Start()
    {
        ColocarInmediatamenteFrenteACamara();
        ActualizarEtiquetasAccesibilidad();
    }

    private void LateUpdate()
    {
        if (siendoAgarrado || !seguirMiradaUsuario || canvasRaiz == null || camaraUsuario == null)
            return;

        Vector3 direccionMirada = camaraUsuario.forward.normalized;
        Vector3 posicionObjetivo = camaraUsuario.position
                                 + (direccionMirada * distanciaAlUsuario)
                                 + (Vector3.up * desplazamientoVertical);

        canvasRaiz.transform.position = Vector3.Lerp(canvasRaiz.transform.position, posicionObjetivo, Time.deltaTime * velocidadSeguimiento);

        Vector3 vectorHaciaCamara = canvasRaiz.transform.position - camaraUsuario.position;
        if (vectorHaciaCamara.sqrMagnitude > 0.001f)
        {
            Quaternion rotacionObjetivo = Quaternion.LookRotation(vectorHaciaCamara, Vector3.up);
            canvasRaiz.transform.rotation = Quaternion.Slerp(canvasRaiz.transform.rotation, rotacionObjetivo, Time.deltaTime * (velocidadSeguimiento + 1.5f));
        }
    }

    private void ColocarInmediatamenteFrenteACamara()
    {
        if (camaraUsuario == null || canvasRaiz == null) return;

        Vector3 frente = camaraUsuario.forward.normalized;
        canvasRaiz.transform.position = camaraUsuario.position
                                        + (frente * distanciaAlUsuario)
                                        + (Vector3.up * desplazamientoVertical);
        canvasRaiz.transform.LookAt(canvasRaiz.transform.position + frente);
    }

    public void AlternarModoSeguimiento()
    {
        seguirMiradaUsuario = !seguirMiradaUsuario;
        if (!seguirMiradaUsuario)
        {
            ColocarInmediatamenteFrenteACamara();
        }
        ActualizarEtiquetasAccesibilidad();
    }

    public void AlternarTamanoTexto()
    {
        modoTextoGrande = !modoTextoGrande;
        float factorFuente = modoTextoGrande ? 1.22f : 1.0f;
        float factorCaja = modoTextoGrande ? 1.13f : 1.0f;

        foreach (var item in textosRegistrados)
        {
            if (item.tmp != null)
                item.tmp.fontSize = item.tamanoBase * factorFuente;
        }

        foreach (var item in rectsRegistrados)
        {
            if (item.rt != null)
            {
                item.rt.sizeDelta = item.tamanoBase * factorCaja;
                item.rt.anchoredPosition = item.posBase * factorCaja;
            }
        }

        ActualizarEtiquetasAccesibilidad();
    }

    private void ActualizarEtiquetasAccesibilidad()
    {
        if (txtBtnSeguimiento != null)
            txtBtnSeguimiento.text = seguirMiradaUsuario ? "👁 Seguir: SÍ" : "📌 Fijo: SÍ";

        if (txtBtnTextoGrande != null)
            txtBtnTextoGrande.text = modoTextoGrande ? "A− Normal" : "A+ Texto";
    }

    private void ConstruirMenuInicial()
    {
        textosRegistrados.Clear();
        rectsRegistrados.Clear();

        // Dimensiones mas largas y amplias para mayor legibilidad
        float anchoCanvas = 1240f;
        float anchoPanel = 1160f;
        float anchoTarjeta = 1070f;
        float altoTarjeta = 136f;
        float espaciado = 22f;

        float altoLista = Mathf.Max(310f, listaEjercicios.Count * (altoTarjeta + espaciado));
        float altoTotalPanel = altoLista + 280f;

        canvasRaiz = new GameObject("HUD_MenuInicial_XR");
        Canvas canvas = canvasRaiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasRaiz.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasRaiz.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rtCanvas = canvasRaiz.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(anchoCanvas, altoTotalPanel + 90f);
        rtCanvas.localScale = Vector3.one * 0.00095f;

        // Barra superior con texto normal (21px) + controles de seguimiento y tamano de texto
        float topY = (altoTotalPanel * 0.5f) + 24f;
        GameObject barraAgarre = CrearPanel(canvasRaiz.transform, "BarraAgarre", new Vector2(-210, topY), new Vector2(540, 44), colTarjetaSecundaria);
        CrearTexto(barraAgarre.transform, "≡  Toma aquí para mover  ≡", 21, FontStyles.Normal, TextAlignmentOptions.Center, colTextoSuave, Vector2.zero, new Vector2(520, 38));

        GameObject btnSeg = CrearBoton(canvasRaiz.transform, "👁 Seguir: SÍ", new Vector2(180, topY), new Vector2(210, 44), colBotonAjuste, 20, AlternarModoSeguimiento);
        txtBtnSeguimiento = btnSeg.GetComponentInChildren<TextMeshProUGUI>();

        GameObject btnTxt = CrearBoton(canvasRaiz.transform, "A+ Texto", new Vector2(405, topY), new Vector2(210, 44), colBotonConfirmar, 20, AlternarTamanoTexto);
        txtBtnTextoGrande = btnTxt.GetComponentInChildren<TextMeshProUGUI>();

        if (permitirAgarrarPanel)
        {
            BoxCollider col = barraAgarre.AddComponent<BoxCollider>();
            col.size = new Vector3(540, 44, 15f);

            Rigidbody rb = canvasRaiz.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            XRGrabInteractable grab = canvasRaiz.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.colliders.Clear();
            grab.colliders.Add(col);
            grab.selectEntered.AddListener(_ => siendoAgarrado = true);
            grab.selectExited.AddListener(_ => siendoAgarrado = false);
        }

        // Panel principal
        GameObject panelPrincipal = CrearPanel(canvasRaiz.transform, "Panel_MenuEjercicios", new Vector2(0, -14), new Vector2(anchoPanel, altoTotalPanel), colFondoPrincipal);

        float cursorY = (altoTotalPanel * 0.5f) - 52f;
        CrearTexto(panelPrincipal.transform, "REHABILITACIÓN DE HOMBRO", 34, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(0, cursorY), new Vector2(1060, 48));

        cursorY -= 44f;
        CrearTexto(panelPrincipal.transform, "Selecciona el ejercicio que deseas realizar hoy", 23, FontStyles.Normal, TextAlignmentOptions.Center, colTextoSuave, new Vector2(0, cursorY), new Vector2(1060, 36));

        // Generar tarjetas de la lista de ejercicios
        cursorY -= 96f;
        for (int i = 0; i < listaEjercicios.Count; i++)
        {
            OpcionEjercicio opcion = listaEjercicios[i];
            CrearTarjetaEjercicio(panelPrincipal.transform, opcion, new Vector2(0, cursorY), new Vector2(anchoTarjeta, altoTarjeta));
            cursorY -= (altoTarjeta + espaciado);
        }

        txtEstadoCarga = CrearTexto(panelPrincipal.transform, "", 20, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.8f, 0.25f), new Vector2(0, cursorY + 25f), new Vector2(1050, 36));

        float bottomY = -(altoTotalPanel * 0.5f) + 50f;
        CrearBoton(panelPrincipal.transform, "SALIR DE LA APLICACIÓN", new Vector2(0, bottomY), new Vector2(460, 56), colBotonSalir, 22, () =>
        {
            Application.Quit();
        });
    }

    private void CrearTarjetaEjercicio(Transform padre, OpcionEjercicio opcion, Vector2 pos, Vector2 tamano)
    {
        GameObject btnObj = new GameObject("Opcion_" + opcion.tituloEjercicio, typeof(RectTransform));
        btnObj.transform.SetParent(padre, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        RegistrarRect(rt, tamano, pos);

        Image img = btnObj.AddComponent<Image>();
        img.color = colBotonEjercicio;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock colores = btn.colors;
        colores.highlightedColor = colAcentoPrimario;
        colores.pressedColor = colDestacadoVerde;
        btn.colors = colores;

        string escenaDestino = opcion.nombreEscena;
        btn.onClick.AddListener(() => CargarEscenaEjercicio(escenaDestino));

        CrearTexto(btnObj.transform, opcion.tituloEjercicio, 27, FontStyles.Bold, TextAlignmentOptions.Left, colTextoClaro, new Vector2(-85, 26), new Vector2(840, 44));
        CrearTexto(btnObj.transform, opcion.descripcionCorta, 21, FontStyles.Normal, TextAlignmentOptions.Left, colTextoSuave, new Vector2(-85, -24), new Vector2(840, 40));
        CrearTexto(btnObj.transform, "INICIAR  ▶", 23, FontStyles.Bold, TextAlignmentOptions.Right, colDestacadoVerde, new Vector2(420, 0), new Vector2(170, 46));
    }

    private void CargarEscenaEjercicio(string nombreEscena)
    {
        if (string.IsNullOrEmpty(nombreEscena))
        {
            if (txtEstadoCarga != null)
                txtEstadoCarga.text = "⚠ Asigna el nombre de la escena en el Inspector.";
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            if (txtEstadoCarga != null)
                txtEstadoCarga.text = "Cargando ejercicio...";
            SceneManager.LoadScene(nombreEscena);
        }
        else
        {
            if (txtEstadoCarga != null)
                txtEstadoCarga.text = $"⚠ La escena '{nombreEscena}' no está agregada en Build Settings.";
        }
    }

    #region Helpers de Creacion UI Responsiva

    private void RegistrarRect(RectTransform rt, Vector2 tamanoBase, Vector2 posBase)
    {
        rectsRegistrados.Add(new RectEscalable { rt = rt, tamanoBase = tamanoBase, posBase = posBase });
    }

    private GameObject CrearPanel(Transform padre, string nombre, Vector2 pos, Vector2 tamano, Color colorFondo)
    {
        GameObject obj = new GameObject(nombre, typeof(RectTransform));
        obj.transform.SetParent(padre, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        RegistrarRect(rt, tamano, pos);

        Image img = obj.AddComponent<Image>();
        img.color = colorFondo;
        return obj;
    }

    private TextMeshProUGUI CrearTexto(Transform padre, string contenido, float tamanoFuente, FontStyles estilo, TextAlignmentOptions alineacion, Color color, Vector2 pos, Vector2 tamano)
    {
        GameObject obj = new GameObject("TextoTMP", typeof(RectTransform));
        obj.transform.SetParent(padre, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        RegistrarRect(rt, tamano, pos);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (fuenteUI != null)
            tmp.font = fuenteUI;

        tmp.text = contenido;
        tmp.fontSize = tamanoFuente;
        tmp.fontStyle = estilo;
        tmp.alignment = alineacion;
        tmp.color = color;

        textosRegistrados.Add(new TextoEscalable { tmp = tmp, tamanoBase = tamanoFuente });
        return tmp;
    }

    private GameObject CrearBoton(Transform padre, string textoBoton, Vector2 pos, Vector2 tamano, Color colorFondo, float tamanoFuente, UnityEngine.Events.UnityAction accionClick)
    {
        GameObject btnObj = new GameObject("Btn_" + textoBoton, typeof(RectTransform));
        btnObj.transform.SetParent(padre, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tamano;
        RegistrarRect(rt, tamano, pos);

        Image img = btnObj.AddComponent<Image>();
        img.color = colorFondo;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(accionClick);

        CrearTexto(btnObj.transform, textoBoton, tamanoFuente, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, Vector2.zero, tamano);
        return btnObj;
    }

    #endregion
}