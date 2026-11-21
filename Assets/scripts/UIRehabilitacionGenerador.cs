using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;

public class UIRehabilitacionGenerador : MonoBehaviour
{
    [Header("Conexion Automatica o Manual con el Ejercicio")]
    public MonoBehaviour scriptEjercicioActual;
    public Transform camaraUsuario;

    [Header("Tipografia Personalizada")]
    public TMP_FontAsset fuenteUI;

    [Header("Navegacion entre Escenas")]
    public string nombreEscenaMenuPrincipal = "menu";

    private IEjercicioRehabilitacion ejercicio;

    [Header("Seguimiento Espacial y Comportamiento")]
    public float distanciaAlUsuario = 1.25f;
    public float elevacionSobreLinea = -0.05f;
    public float velocidadSeguimiento = 4.5f;
    public bool permitirAgarrarPanel = true;
    [Tooltip("Activo: sigue suavemente tu mirada. Inactivo: permanece estatico frente a ti.")]
    public bool seguirMiradaUsuario = true;

    // Paleta de colores amigable y de alto contraste
    private readonly Color colFondoPrincipal = new Color(0.01f, 0.12f, 0.37f, 0.94f);
    private readonly Color colTarjetaSecundaria = new Color(0.06f, 0.18f, 0.46f, 0.96f);
    private readonly Color colAcentoPrimario = new Color(0.14f, 0.44f, 0.91f, 1f);
    private readonly Color colBotonConfirmar = new Color(0.07f, 0.56f, 0.38f, 1f);
    private readonly Color colBotonCancelar = new Color(0.84f, 0.06f, 0.09f, 1f);
    private readonly Color colBotonAjuste = new Color(0.17f, 0.29f, 0.55f, 1f);
    private readonly Color colTextoClaro = new Color(1f, 1f, 1f, 1f);
    private readonly Color colTextoSuave = new Color(0.84f, 0.86f, 0.90f, 1f);
    private readonly Color colDestacadoVerde = new Color(0.18f, 0.90f, 0.62f, 1f);
    private readonly Color colAlertaRoja = new Color(1f, 0f, 0f, 0.96f);

    // Contenedores principales
    private GameObject canvasRaiz;
    private GameObject vistaConfiguracionCentrada;
    private GameObject btnOpcionesAvanzadasExterno;
    private GameObject vistaOpcionesAvanzadas;
    private GameObject vistaCalibracionHombros;
    private GameObject vistaEjercicioActivo;
    private GameObject vistaReporteFinal;
    private GameObject panelAlertaDesvio;

    // Controles globales superiores
    private TextMeshProUGUI txtBtnSeguimiento;
    private TextMeshProUGUI txtBtnTextoGrande;

    // Textos de Configuracion
    private TextMeshProUGUI txtValSeries, txtValReps, txtValDescanso, txtValAngulo;
    private TextMeshProUGUI txtSubtituloConfig;

    // Textos de Opciones Avanzadas (en cm)
    private TextMeshProUGUI txtValGrosorObj;
    private TextMeshProUGUI txtValGrosorAccion;
    private TextMeshProUGUI txtValTolerancia;
    private TextMeshProUGUI txtValZonaPerfecta;
    private TextMeshProUGUI txtValMargenBorde;
    private TextMeshProUGUI txtValDistPunto;
    private TextMeshProUGUI txtValAjusteHombro;

    // Textos de Calibracion de Hombros (Fisioterapeuta)
    private TextMeshProUGUI txtInstruccionHombros;
    private TextMeshProUGUI txtEstadoHombroDer;
    private TextMeshProUGUI txtEstadoHombroIzq;

    // Textos de Ejercicio Activo
    private TextMeshProUGUI txtDatoSerie, txtDatoReps, txtDatoPrecision, txtDatoTimer;
    private TextMeshProUGUI txtApoyoInstruccion;
    private GameObject btnSiguienteSerieObj;

    // Textos de Reporte Final
    private TextMeshProUGUI txtReportePorcentajeGrande;
    private TextMeshProUGUI txtReporteDetalle;

    private bool siendoAgarrado = false;
    private bool ancladoACentroLinea = false;
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

        VincularEjercicio();
        ConstruirInterfazCompleta();
    }

    private void VincularEjercicio()
    {
        if (scriptEjercicioActual != null && scriptEjercicioActual is IEjercicioRehabilitacion ejAsignado)
        {
            ejercicio = ejAsignado;
            return;
        }

        foreach (MonoBehaviour mb in GetComponents<MonoBehaviour>())
        {
            if (mb is IEjercicioRehabilitacion ejLocal)
            {
                ejercicio = ejLocal;
                scriptEjercicioActual = mb;
                return;
            }
        }

        foreach (MonoBehaviour mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (mb is IEjercicioRehabilitacion ejEscena)
            {
                ejercicio = ejEscena;
                scriptEjercicioActual = mb;
                return;
            }
        }
    }

    private void Start()
    {
        ColocarInmediatamenteFrenteACamara();
        ActualizarEtiquetasAccesibilidad();

        if (ejercicio != null)
        {
            ActualizarValoresConfig(
                ejercicio.SeriesObjetivo,
                ejercicio.RepeticionesObjetivo,
                ejercicio.TiempoDescansoSegundos,
                ejercicio.AnguloObjetivo
            );
        }
        RefrescarTextosOpcionesAvanzadas();
    }

    private void LateUpdate()
    {
        if (siendoAgarrado || !seguirMiradaUsuario || canvasRaiz == null || camaraUsuario == null)
            return;

        Vector3 direccionMirada = camaraUsuario.forward.normalized;
        float compensacionVertical = ancladoACentroLinea ? elevacionSobreLinea : 0f;

        Vector3 posicionObjetivo = camaraUsuario.position
                                 + (direccionMirada * distanciaAlUsuario)
                                 + (Vector3.up * compensacionVertical);

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
        float compensacionVertical = ancladoACentroLinea ? elevacionSobreLinea : 0f;
        canvasRaiz.transform.position = camaraUsuario.position + (frente * distanciaAlUsuario) + (Vector3.up * compensacionVertical);
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

    private void VolverAlMenuPrincipal()
    {
        if (!string.IsNullOrEmpty(nombreEscenaMenuPrincipal) && Application.CanStreamedLevelBeLoaded(nombreEscenaMenuPrincipal))
        {
            SceneManager.LoadScene(nombreEscenaMenuPrincipal);
        }
        else if (txtSubtituloConfig != null)
        {
            txtSubtituloConfig.color = new Color(1f, 0.8f, 0.25f);
            txtSubtituloConfig.text = $"⚠ Agrega '{nombreEscenaMenuPrincipal}' en Build Settings";
        }
    }

    #region Construccion Procedimental del UI

    private void ConstruirInterfazCompleta()
    {
        textosRegistrados.Clear();
        rectsRegistrados.Clear();

        canvasRaiz = new GameObject("HUD_Rehabilitacion_XR");
        Canvas canvas = canvasRaiz.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        CanvasScaler scaler = canvasRaiz.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasRaiz.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rtCanvas = canvasRaiz.GetComponent<RectTransform>();
        rtCanvas.sizeDelta = new Vector2(1060, 880);
        rtCanvas.localScale = Vector3.one * 0.00095f;

        // Barra superior con texto tamaño normal (21px) + botones Seguir/Fijo y A+ Texto
        GameObject barraAgarre = CrearPanel(canvasRaiz.transform, "BarraAgarre", new Vector2(-210, 355), new Vector2(490, 44), colTarjetaSecundaria);
        CrearTexto(barraAgarre.transform, "≡  Toma aquí para mover  ≡", 21, FontStyles.Normal, TextAlignmentOptions.Center, colTextoSuave, Vector2.zero, new Vector2(470, 38));

        GameObject btnSeg = CrearBoton(canvasRaiz.transform, "👁 Seguir: SÍ", new Vector2(155, 355), new Vector2(195, 44), colBotonAjuste, 20, AlternarModoSeguimiento);
        txtBtnSeguimiento = btnSeg.GetComponentInChildren<TextMeshProUGUI>();

        GameObject btnTxt = CrearBoton(canvasRaiz.transform, "A+ Texto", new Vector2(365, 355), new Vector2(195, 44), colBotonConfirmar, 20, AlternarTamanoTexto);
        txtBtnTextoGrande = btnTxt.GetComponentInChildren<TextMeshProUGUI>();

        if (permitirAgarrarPanel)
        {
            BoxCollider col = barraAgarre.AddComponent<BoxCollider>();
            col.size = new Vector3(490, 44, 15f);

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

        ConstruirVistaConfiguracionCentrada();
        ConstruirVistaOpcionesAvanzadas();
        ConstruirVistaCalibracionHombros();
        ConstruirVistaEjercicioActivo();
        ConstruirVistaReporteFinal();

        // Banner de alerta reubicado abajo para no obstruir botones superiores
        panelAlertaDesvio = CrearPanel(canvasRaiz.transform, "PanelAlertaDesvio", new Vector2(0, -345), new Vector2(680, 96), colAlertaRoja);
        CrearTexto(panelAlertaDesvio.transform, "⚠ ¡TE ESTÁS DESVIANDO, CUIDADO!\nMira tu mano y regresa a la línea guía", 24, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, Vector2.zero, new Vector2(650, 88));
        panelAlertaDesvio.SetActive(false);
    }

    private void ConstruirVistaConfiguracionCentrada()
    {
        vistaConfiguracionCentrada = CrearPanel(canvasRaiz.transform, "Vista_ConfiguracionCentrada", new Vector2(0, -10), new Vector2(860, 620), colFondoPrincipal);

        CrearBoton(vistaConfiguracionCentrada.transform, "←  MENÚ", new Vector2(-330, 265), new Vector2(155, 46), colBotonAjuste, 20, () =>
        {
            VolverAlMenuPrincipal();
        });

        CrearTexto(vistaConfiguracionCentrada.transform, "CONFIGURACIÓN DEL EJERCICIO", 29, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(75, 265), new Vector2(610, 46));
        txtSubtituloConfig = CrearTexto(vistaConfiguracionCentrada.transform, "Ajusta los valores indicados por tu fisioterapeuta", 21, FontStyles.Normal, TextAlignmentOptions.Center, colTextoSuave, new Vector2(0, 215), new Vector2(780, 34));

        txtValSeries = CrearFilaAjusteAmplia(vistaConfiguracionCentrada.transform, "Series a realizar:", 130, () => ejercicio?.ModificarSeries(-1), () => ejercicio?.ModificarSeries(1));
        txtValReps = CrearFilaAjusteAmplia(vistaConfiguracionCentrada.transform, "Repeticiones por serie:", 45, () => ejercicio?.ModificarRepeticiones(-1), () => ejercicio?.ModificarRepeticiones(1));
        txtValDescanso = CrearFilaAjusteAmplia(vistaConfiguracionCentrada.transform, "Descanso entre series:", -40, () => ejercicio?.ModificarDescanso(-5f), () => ejercicio?.ModificarDescanso(5f));
        txtValAngulo = CrearFilaAjusteAmplia(vistaConfiguracionCentrada.transform, "Ángulo de apertura:", -125, () => ejercicio?.ModificarAngulo(-5f), () => ejercicio?.ModificarAngulo(5f));

        CrearBoton(vistaConfiguracionCentrada.transform, "CONTINUAR A CALIBRACIÓN", new Vector2(0, -230), new Vector2(560, 70), colBotonConfirmar, 25, () =>
        {
            ejercicio?.ConfirmarConfiguracionYComenzar();
        });

        btnOpcionesAvanzadasExterno = CrearBoton(canvasRaiz.transform, "⚙  OPCIONES AVANZADAS (CM)", new Vector2(0, -365), new Vector2(430, 50), colTarjetaSecundaria, 20, () =>
        {
            AbrirOpcionesAvanzadas();
        });
    }

    private void ConstruirVistaOpcionesAvanzadas()
    {
        vistaOpcionesAvanzadas = CrearPanel(canvasRaiz.transform, "Vista_OpcionesAvanzadas", new Vector2(0, -25), new Vector2(880, 700), colFondoPrincipal);

        CrearBoton(vistaOpcionesAvanzadas.transform, "←  VOLVER", new Vector2(-340, 305), new Vector2(155, 44), colBotonAjuste, 20, () =>
        {
            CerrarOpcionesAvanzadas();
        });

        CrearTexto(vistaOpcionesAvanzadas.transform, "OPCIONES AVANZADAS (EN CM)", 28, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(75, 305), new Vector2(610, 44));
        CrearTexto(vistaOpcionesAvanzadas.transform, "Ajuste fino de grosores, tolerancias y eje anatómico en centímetros", 19, FontStyles.Normal, TextAlignmentOptions.Center, colTextoSuave, new Vector2(0, 260), new Vector2(820, 32));

        txtValGrosorObj = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Grosor línea objetivo:",
            195,
            () => ModificarCampoEnCm("grosorLineaObjetivo", -0.5f, 1.0f, 15.0f),
            () => ModificarCampoEnCm("grosorLineaObjetivo", 0.5f, 1.0f, 15.0f)
        );

        txtValGrosorAccion = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Grosor línea acción:",
            130,
            () => ModificarCampoEnCm("grosorLineaAccion", -0.5f, 0.5f, 10.0f),
            () => ModificarCampoEnCm("grosorLineaAccion", 0.5f, 0.5f, 10.0f)
        );

        txtValTolerancia = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Tolerancia desviación:",
            65,
            () => ModificarCampoEnCm("toleranciaDesviacion", -1.0f, 2.0f, 35.0f),
            () => ModificarCampoEnCm("toleranciaDesviacion", 1.0f, 2.0f, 35.0f)
        );

        txtValZonaPerfecta = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Zona perfecta (100%):",
            0,
            () => ModificarCampoEnCm("zonaPerfecta", -0.5f, 1.0f, 25.0f),
            () => ModificarCampoEnCm("zonaPerfecta", 0.5f, 1.0f, 25.0f)
        );

        txtValMargenBorde = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Margen llegada borde:",
            -65,
            () => ModificarCampoEnCm("margenLlegadaBorde", -0.5f, 2.0f, 20.0f),
            () => ModificarCampoEnCm("margenLlegadaBorde", 0.5f, 2.0f, 20.0f)
        );

        txtValDistPunto = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Distancia mínima punto:",
            -130,
            () => ModificarCampoEnCm("distanciaMinimaPunto", -0.5f, 0.5f, 10.0f),
            () => ModificarCampoEnCm("distanciaMinimaPunto", 0.5f, 0.5f, 10.0f)
        );

        // Nuevo parametro anatomico: desplazamiento interno hacia el centro de la articulacion del hombro
        txtValAjusteHombro = CrearFilaAjusteAvanzada(
            vistaOpcionesAvanzadas.transform,
            "Ajuste interno hombro:",
            -195,
            () => ModificarCampoEnCm("desplazamientoInternoHombro", -0.5f, 0.0f, 12.0f),
            () => ModificarCampoEnCm("desplazamientoInternoHombro", 0.5f, 0.0f, 12.0f)
        );

        CrearBoton(vistaOpcionesAvanzadas.transform, "LISTO Y VOLVER", new Vector2(0, -280), new Vector2(400, 56), colBotonConfirmar, 22, () =>
        {
            CerrarOpcionesAvanzadas();
        });

        vistaOpcionesAvanzadas.SetActive(false);
    }

    private void AbrirOpcionesAvanzadas()
    {
        RefrescarTextosOpcionesAvanzadas();
        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(false);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(false);
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(true);
    }

    private void CerrarOpcionesAvanzadas()
    {
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(false);
        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(true);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(true);
    }

    private void ModificarCampoEnCm(string nombreCampo, float deltaCm, float minCm, float maxCm)
    {
        if (scriptEjercicioActual == null) return;

        FieldInfo campo = scriptEjercicioActual.GetType().GetField(nombreCampo, BindingFlags.Public | BindingFlags.Instance);
        if (campo != null && campo.FieldType == typeof(float))
        {
            float valorMetros = (float)campo.GetValue(scriptEjercicioActual);
            float valorCm = Mathf.Round((valorMetros * 100f) * 10f) / 10f;
            float nuevoCm = Mathf.Clamp(valorCm + deltaCm, minCm, maxCm);
            campo.SetValue(scriptEjercicioActual, nuevoCm / 100f);
        }

        RefrescarTextosOpcionesAvanzadas();
    }

    private float ObtenerValorEnCm(string nombreCampo)
    {
        if (scriptEjercicioActual == null) return 0f;

        FieldInfo campo = scriptEjercicioActual.GetType().GetField(nombreCampo, BindingFlags.Public | BindingFlags.Instance);
        if (campo != null && campo.FieldType == typeof(float))
        {
            return (float)campo.GetValue(scriptEjercicioActual) * 100f;
        }
        return 0f;
    }

    private void RefrescarTextosOpcionesAvanzadas()
    {
        if (txtValGrosorObj != null) txtValGrosorObj.text = $"{ObtenerValorEnCm("grosorLineaObjetivo"):0.#} cm";
        if (txtValGrosorAccion != null) txtValGrosorAccion.text = $"{ObtenerValorEnCm("grosorLineaAccion"):0.#} cm";
        if (txtValTolerancia != null) txtValTolerancia.text = $"{ObtenerValorEnCm("toleranciaDesviacion"):0.#} cm";
        if (txtValZonaPerfecta != null) txtValZonaPerfecta.text = $"{ObtenerValorEnCm("zonaPerfecta"):0.#} cm";
        if (txtValMargenBorde != null) txtValMargenBorde.text = $"{ObtenerValorEnCm("margenLlegadaBorde"):0.#} cm";
        if (txtValDistPunto != null) txtValDistPunto.text = $"{ObtenerValorEnCm("distanciaMinimaPunto"):0.#} cm";
        if (txtValAjusteHombro != null) txtValAjusteHombro.text = $"{ObtenerValorEnCm("desplazamientoInternoHombro"):0.#} cm";
    }

    private void ConstruirVistaCalibracionHombros()
    {
        vistaCalibracionHombros = CrearPanel(canvasRaiz.transform, "Vista_CalibracionHombros", new Vector2(0, -10), new Vector2(860, 600), colFondoPrincipal);

        CrearBoton(vistaCalibracionHombros.transform, "←  VOLVER", new Vector2(-330, 255), new Vector2(155, 44), colBotonAjuste, 20, () =>
        {
            ejercicio?.MostrarPanelConfiguracion();
        });

        CrearTexto(vistaCalibracionHombros.transform, "CALIBRACIÓN DE HOMBROS", 29, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(75, 255), new Vector2(610, 46));

        GameObject tarjetaIndicaciones = CrearPanel(vistaCalibracionHombros.transform, "TarjetaIndicacionesFisio", new Vector2(0, 110), new Vector2(780, 185), colTarjetaSecundaria);
        txtInstruccionHombros = CrearTexto(
            tarjetaIndicaciones.transform,
            "INDICACIONES PARA EL FISIOTERAPEUTA:\n1. Coloca los lentes al paciente y pídele mantenerse en su lugar.\n2. Toca su HOMBRO DERECHO con un mando y presiona el gatillo.",
            23, FontStyles.Bold, TextAlignmentOptions.Center, colTextoClaro, Vector2.zero, new Vector2(740, 170)
        );

        GameObject filaDer = CrearPanel(vistaCalibracionHombros.transform, "EstadoHombroDer", new Vector2(0, -30), new Vector2(780, 68), colTarjetaSecundaria);
        txtEstadoHombroDer = CrearTexto(filaDer.transform, "1. Hombro Derecho:   ⏳ Pendiente (toca y jala gatillo)", 23, FontStyles.Bold, TextAlignmentOptions.Left, new Color(1f, 0.82f, 0.28f), new Vector2(15, 0), new Vector2(730, 56));

        GameObject filaIzq = CrearPanel(vistaCalibracionHombros.transform, "EstadoHombroIzq", new Vector2(0, -115), new Vector2(780, 68), colTarjetaSecundaria);
        txtEstadoHombroIzq = CrearTexto(filaIzq.transform, "2. Hombro Izquierdo:   ⏸ En espera", 23, FontStyles.Normal, TextAlignmentOptions.Left, colTextoSuave, new Vector2(15, 0), new Vector2(730, 56));

        CrearTexto(vistaCalibracionHombros.transform, "⚠ Importante: El paciente debe permanecer en su lugar sin caminar.", 20, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.28f), new Vector2(0, -180), new Vector2(780, 34));

        vistaCalibracionHombros.SetActive(false);
    }

    private void ConstruirVistaEjercicioActivo()
    {
        vistaEjercicioActivo = CrearPanel(canvasRaiz.transform, "Vista_EjercicioActivo", new Vector2(0, -15), new Vector2(680, 620), colFondoPrincipal);

        // Fila superior con dos botones claros: Recalibrar (izquierda) y Cancelar (derecha)
        CrearBoton(vistaEjercicioActivo.transform, "↻  RECALIBRAR", new Vector2(-150, 265), new Vector2(280, 50), colBotonAjuste, 20, () =>
        {
            ejercicio?.RecalibrarEjercicio();
        });

        CrearBoton(vistaEjercicioActivo.transform, "✕  CANCELAR", new Vector2(150, 265), new Vector2(280, 50), colBotonCancelar, 20, () =>
        {
            ejercicio?.CancelarEjercicio();
        });

        CrearTexto(vistaEjercicioActivo.transform, "PROGRESO EN VIVO", 24, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(0, 210), new Vector2(600, 36));

        txtDatoSerie = CrearTexto(vistaEjercicioActivo.transform, "Serie: 1 / 3", 31, FontStyles.Bold, TextAlignmentOptions.Left, colTextoClaro, new Vector2(0, 160), new Vector2(590, 42));
        txtDatoReps = CrearTexto(vistaEjercicioActivo.transform, "Repeticiones: 0 / 10", 31, FontStyles.Bold, TextAlignmentOptions.Left, colTextoClaro, new Vector2(0, 112), new Vector2(590, 42));
        txtDatoPrecision = CrearTexto(vistaEjercicioActivo.transform, "Precisión: 0.0%", 31, FontStyles.Bold, TextAlignmentOptions.Left, colDestacadoVerde, new Vector2(0, 64), new Vector2(590, 42));
        txtDatoTimer = CrearTexto(vistaEjercicioActivo.transform, "", 26, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 0.82f, 0.28f), new Vector2(0, 20), new Vector2(590, 36));

        CrearPanel(vistaEjercicioActivo.transform, "Separador", new Vector2(0, -4), new Vector2(590, 3), new Color(1f, 1f, 1f, 0.15f));

        GameObject cajaApoyo = CrearPanel(vistaEjercicioActivo.transform, "CajaTextosApoyo", new Vector2(0, -100), new Vector2(610, 158), colTarjetaSecundaria);
        txtApoyoInstruccion = CrearTexto(cajaApoyo.transform, "Mira tu mano, colócala en posición inicial\ny jala el gatillo para proyectar tu recorrido", 24, FontStyles.Bold, TextAlignmentOptions.Center, colTextoClaro, Vector2.zero, new Vector2(580, 145));

        btnSiguienteSerieObj = CrearBoton(vistaEjercicioActivo.transform, "INICIAR SIGUIENTE SERIE", new Vector2(0, -225), new Vector2(580, 56), colAcentoPrimario, 22, () =>
        {
            ejercicio?.SaltarSiguienteSerie();
        });
        btnSiguienteSerieObj.SetActive(false);

        vistaEjercicioActivo.SetActive(false);
    }

    private void ConstruirVistaReporteFinal()
    {
        vistaReporteFinal = CrearPanel(canvasRaiz.transform, "Vista_ReporteFinalCentrado", new Vector2(0, -10), new Vector2(880, 600), colFondoPrincipal);

        CrearTexto(vistaReporteFinal.transform, "REPORTE DE SESIÓN COMPLETADA", 32, FontStyles.Bold, TextAlignmentOptions.Center, colAcentoPrimario, new Vector2(0, 240), new Vector2(800, 48));

        GameObject tarjetaPorcentaje = CrearPanel(vistaReporteFinal.transform, "TarjetaPorcentaje", new Vector2(0, 95), new Vector2(600, 190), colTarjetaSecundaria);
        CrearTexto(tarjetaPorcentaje.transform, "PRECISIÓN GLOBAL ALCANZADA", 22, FontStyles.Bold, TextAlignmentOptions.Center, colTextoSuave, new Vector2(0, 65), new Vector2(560, 34));
        txtReportePorcentajeGrande = CrearTexto(tarjetaPorcentaje.transform, "100.0%", 88, FontStyles.Bold, TextAlignmentOptions.Center, colDestacadoVerde, new Vector2(0, -15), new Vector2(560, 115));

        txtReporteDetalle = CrearTexto(vistaReporteFinal.transform, "Completaste 3 series de 10 repeticiones (45°)", 26, FontStyles.Normal, TextAlignmentOptions.Center, colTextoClaro, new Vector2(0, -35), new Vector2(800, 44));

        GameObject cajaAvisoFisio = CrearPanel(vistaReporteFinal.transform, "CajaAvisoFisioterapeuta", new Vector2(0, -120), new Vector2(780, 95), new Color(0.16f, 0.36f, 0.56f, 0.95f));
        CrearTexto(cajaAvisoFisio.transform, "📋 Por favor, avisa al fisioterapeuta a cargo\npara que registre el resultado obtenido.", 25, FontStyles.Bold, TextAlignmentOptions.Center, Color.white, Vector2.zero, new Vector2(740, 84));

        CrearBoton(vistaReporteFinal.transform, "REGRESAR AL MENÚ PRINCIPAL", new Vector2(0, -225), new Vector2(580, 64), colBotonConfirmar, 25, () =>
        {
            VolverAlMenuPrincipal();
        });

        vistaReporteFinal.SetActive(false);
    }

    #endregion

    #region Metodos Publicos llamados por cualquier Ejercicio

    public void MostrarModoConfiguracion(int series, int reps, float descanso, float angulo)
    {
        ancladoACentroLinea = false;

        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(true);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(true);
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(false);
        if (vistaCalibracionHombros != null) vistaCalibracionHombros.SetActive(false);
        if (vistaEjercicioActivo != null) vistaEjercicioActivo.SetActive(false);
        if (vistaReporteFinal != null) vistaReporteFinal.SetActive(false);
        if (panelAlertaDesvio != null) panelAlertaDesvio.SetActive(false);

        if (txtSubtituloConfig != null)
        {
            txtSubtituloConfig.color = colTextoSuave;
            txtSubtituloConfig.text = "Ajusta los valores indicados por tu fisioterapeuta";
        }

        ActualizarValoresConfig(series, reps, descanso, angulo);
    }

    public void MostrarModoCalibracionHombros(bool derechoRegistrado, bool izquierdoRegistrado)
    {
        ancladoACentroLinea = false;

        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(false);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(false);
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(false);
        if (vistaCalibracionHombros != null) vistaCalibracionHombros.SetActive(true);
        if (vistaEjercicioActivo != null) vistaEjercicioActivo.SetActive(false);
        if (vistaReporteFinal != null) vistaReporteFinal.SetActive(false);
        if (panelAlertaDesvio != null) panelAlertaDesvio.SetActive(false);

        if (!derechoRegistrado)
        {
            if (txtInstruccionHombros != null)
                txtInstruccionHombros.text = "INDICACIONES PARA EL FISIOTERAPEUTA:\n1. Coloca los lentes al paciente y entrega los mandos.\n2. Toca su HOMBRO DERECHO con un mando y presiona el gatillo.";

            if (txtEstadoHombroDer != null)
            {
                txtEstadoHombroDer.color = new Color(1f, 0.82f, 0.28f);
                txtEstadoHombroDer.text = "1. Hombro Derecho:   ⏳ Toca el hombro derecho y jala el gatillo";
            }
            if (txtEstadoHombroIzq != null)
            {
                txtEstadoHombroIzq.color = colTextoSuave;
                txtEstadoHombroIzq.text = "2. Hombro Izquierdo:   ⏸ En espera";
            }
        }
        else if (!izquierdoRegistrado)
        {
            if (txtInstruccionHombros != null)
                txtInstruccionHombros.text = "¡HOMBRO DERECHO REGISTRADO!\nAhora toca su HOMBRO IZQUIERDO con cualquiera de los mandos y presiona el gatillo.";

            if (txtEstadoHombroDer != null)
            {
                txtEstadoHombroDer.color = colDestacadoVerde;
                txtEstadoHombroDer.text = "1. Hombro Derecho:   ✔ Registrado";
            }
            if (txtEstadoHombroIzq != null)
            {
                txtEstadoHombroIzq.color = new Color(1f, 0.82f, 0.28f);
                txtEstadoHombroIzq.text = "2. Hombro Izquierdo:   ⏳ Toca el hombro izquierdo y jala el gatillo";
            }
        }
    }

    public void ActualizarValoresConfig(int series, int reps, float descanso, float angulo)
    {
        if (txtValSeries != null) txtValSeries.text = series.ToString();
        if (txtValReps != null) txtValReps.text = reps.ToString();
        if (txtValDescanso != null) txtValDescanso.text = $"{descanso}s";
        if (txtValAngulo != null) txtValAngulo.text = $"{angulo}°";
    }

    public void MostrarModoEsperandoCalibracion()
    {
        ancladoACentroLinea = false;

        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(false);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(false);
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(false);
        if (vistaCalibracionHombros != null) vistaCalibracionHombros.SetActive(false);
        if (vistaEjercicioActivo != null) vistaEjercicioActivo.SetActive(true);
        if (vistaReporteFinal != null) vistaReporteFinal.SetActive(false);
        if (panelAlertaDesvio != null) panelAlertaDesvio.SetActive(false);
        if (btnSiguienteSerieObj != null) btnSiguienteSerieObj.SetActive(false);

        ActualizarMensajeApoyo("HOMBROS LISTOS\nMira tu mano, colócala en posición inicial y jala el gatillo");
    }

    public void EnfocarCentroLineaObjetivo(Vector3 puntoMedioLinea)
    {
        ancladoACentroLinea = true;
    }

    public void ActualizarDatosEjercicio(string instruccionApoyo, int serieAct, int serieObj, int repAct, int repObj, float precision)
    {
        if (txtDatoSerie != null) txtDatoSerie.text = $"Serie: {serieAct} / {serieObj}";
        if (txtDatoReps != null) txtDatoReps.text = $"Repeticiones: {repAct} / {repObj}";
        if (txtDatoPrecision != null) txtDatoPrecision.text = $"Precisión: {precision:F1}%";

        ActualizarMensajeApoyo(instruccionApoyo);
    }

    public void ActualizarMensajeApoyo(string mensaje)
    {
        if (txtApoyoInstruccion == null) return;

        // Refuerza visualmente que mire su mano y siga el recorrido proyectado frente a el
        if (!string.IsNullOrEmpty(mensaje) && !mensaje.Contains("Mira tu mano"))
        {
            txtApoyoInstruccion.text = $"{mensaje}\n👀 Mira tu mano y sigue la línea guía";
        }
        else
        {
            txtApoyoInstruccion.text = mensaje;
        }
    }

    public void MostrarAlertaDesviacion(bool desviado)
    {
        if (panelAlertaDesvio != null && panelAlertaDesvio.activeSelf != desviado)
        {
            panelAlertaDesvio.SetActive(desviado);
        }
    }

    public void MostrarModoDescanso(bool enDescanso, int segundosRestantes = 0)
    {
        if (btnSiguienteSerieObj != null)
            btnSiguienteSerieObj.SetActive(enDescanso);

        if (txtDatoTimer != null)
            txtDatoTimer.text = enDescanso ? $"Descanso: {segundosRestantes}s" : "";

        if (enDescanso)
            MostrarAlertaDesviacion(false);
    }

    public void MostrarReporteFinal(int seriesCompletadas, int repsPorSerie, float angulo, string brazoTrabajado, float precisionFinal)
    {
        ancladoACentroLinea = false;

        if (vistaConfiguracionCentrada != null) vistaConfiguracionCentrada.SetActive(false);
        if (btnOpcionesAvanzadasExterno != null) btnOpcionesAvanzadasExterno.SetActive(false);
        if (vistaOpcionesAvanzadas != null) vistaOpcionesAvanzadas.SetActive(false);
        if (vistaCalibracionHombros != null) vistaCalibracionHombros.SetActive(false);
        if (vistaEjercicioActivo != null) vistaEjercicioActivo.SetActive(false);
        if (panelAlertaDesvio != null) panelAlertaDesvio.SetActive(false);
        if (vistaReporteFinal != null) vistaReporteFinal.SetActive(true);

        if (txtReportePorcentajeGrande != null)
            txtReportePorcentajeGrande.text = $"{precisionFinal:F1}%";

        if (txtReporteDetalle != null)
            txtReporteDetalle.text = $"Brazo {brazoTrabajado}  •  {seriesCompletadas} series de {repsPorSerie} repeticiones ({angulo}°)";
    }

    #endregion

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

    private TextMeshProUGUI CrearFilaAjusteAmplia(Transform padre, string etiqueta, float posY, UnityEngine.Events.UnityAction alRestar, UnityEngine.Events.UnityAction alSumar)
    {
        CrearTexto(padre, etiqueta, 27, FontStyles.Normal, TextAlignmentOptions.Left, colTextoClaro, new Vector2(-180, posY), new Vector2(360, 54));
        CrearBoton(padre, "−", new Vector2(85, posY), new Vector2(64, 62), colBotonAjuste, 34, alRestar);
        TextMeshProUGUI txtValor = CrearTexto(padre, "0", 30, FontStyles.Bold, TextAlignmentOptions.Center, colTextoClaro, new Vector2(185, posY), new Vector2(115, 54));
        CrearBoton(padre, "+", new Vector2(285, posY), new Vector2(64, 62), colBotonAjuste, 34, alSumar);
        return txtValor;
    }

    private TextMeshProUGUI CrearFilaAjusteAvanzada(Transform padre, string etiqueta, float posY, UnityEngine.Events.UnityAction alRestar, UnityEngine.Events.UnityAction alSumar)
    {
        CrearTexto(padre, etiqueta, 23, FontStyles.Normal, TextAlignmentOptions.Left, colTextoClaro, new Vector2(-200, posY), new Vector2(380, 48));
        CrearBoton(padre, "−", new Vector2(75, posY), new Vector2(56, 52), colBotonAjuste, 30, alRestar);
        TextMeshProUGUI txtValor = CrearTexto(padre, "0 cm", 24, FontStyles.Bold, TextAlignmentOptions.Center, colDestacadoVerde, new Vector2(190, posY), new Vector2(140, 48));
        CrearBoton(padre, "+", new Vector2(305, posY), new Vector2(56, 52), colBotonAjuste, 30, alSumar);
        return txtValor;
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