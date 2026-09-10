using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TP06.Models;

namespace TP06.Controllers;

public class HomeController : Controller
{
    private const string ProgressKey = "escapeProgress";
    private const string WinKey = "escapeWon";
    private static readonly string[] ChallengeOrder =
    {
        "Coudet",
        "Acuna",
        "Demichelis",
        "Tapia",
        "Scaloni",
        "Donofrio",
        "Di Carlo"
    };
    private readonly ILogger<HomeController> _logger;
    private readonly BD _bd;

    public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _bd = new BD(configuration);
    }

    public IActionResult Index()
    {
        var progress = GetProgress();
        ViewBag.Progress = progress;
        ViewBag.CompletedCount = progress.Count;
        ViewBag.Won = IsWon();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Lobby";
        ViewBag.PartidaEnCurso = progress.Count > 0 || IsWon();
        return View();
    }

    [HttpPost]
    public IActionResult CargarProgreso(string codigoSesion)
    {
        if (string.IsNullOrWhiteSpace(codigoSesion))
        {
            TempData["mensajeError"] = "Por favor ingresá un código válido.";
            return RedirectToAction(nameof(Index));
        }

        var resultado = _bd.RecuperarProgresoPorCodigo(codigoSesion.Trim());
        if (resultado == null)
        {
            TempData["mensajeError"] = "El código no es válido o ha expirado.";
            return RedirectToAction(nameof(Index));
        }

        var (nombreParticipante, progreso, tiempoRestante) = resultado.Value;
        
        // Restaurar la sesión del jugador
        HttpContext.Session.SetString("participante", nombreParticipante);
        HttpContext.Session.SetString(ProgressKey, progreso);
        HttpContext.Session.SetString("codigoActual", codigoSesion.Trim());
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);
        
        TempData["mensajeExito"] = $"Bienvenido de vuelta, {nombreParticipante}! Tu progreso ha sido restaurado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult Continuar(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return RedirectToAction(nameof(Index));
        }

        var normalized = codigo.Trim();

        var dest = normalized.ToUpperInvariant() switch
        {
            "CANTILO" => nameof(Coudet),
            "VESTUARIO" => nameof(Acuna),
            "JETLAG" => nameof(Demichelis),
            "FIGURA" => nameof(Tapia),
            "SECUENCIA" => nameof(Scaloni),
            "EQUIPO" => nameof(Donofrio),
            "RIVER" => nameof(DiCarlo),
            _ => nameof(Index)
        };

        // set intro video for the next room when navigating from the index/continuar form
        if (string.Equals(dest, nameof(DiCarlo), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = "/videos/otro-video.mp4";
        }
        else if (!string.Equals(dest, nameof(Index), StringComparison.OrdinalIgnoreCase))
        {
            TempData["ShowIntro"] = "/videos/entrada-carcel.mp4";
        }

        return RedirectToAction(dest);
    }

    public IActionResult Coudet()
    {
        if (!CanAccessChallenge("Coudet"))
        {
            return RedirectToAction(nameof(Index));
        }

        // Inicializar timer si es primera vez
        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizó.";
            return RedirectToAction(nameof(Index));
        }
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);

        var attempts = HttpContext.Session.GetInt32("coudetAttempts") ?? 0;
        var dice = HttpContext.Session.GetString("coudetDice") ?? string.Empty;
        var held = HttpContext.Session.GetString("coudetHeld") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(dice))
        {
            var random = new Random();
            var initial = Enumerable.Range(1, 5).Select(_ => random.Next(1, 7)).ToArray();
            HttpContext.Session.SetString("coudetDice", string.Join(',', initial));
            dice = string.Join(',', initial);
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Coudet";
        ViewBag.PartidaEnCurso = true;
        // show intro only if navigated from another page (TempData) or explicit query param
        if (TempData["ShowIntro"] != null)
        {
            ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        }
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"]))
        {
            ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        }
        ViewBag.Attempts = attempts;
        ViewBag.Dice = dice;
        ViewBag.Held = held;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Coudet(string accion, string dados, string held, int intentos)
    {
        if (!CanAccessChallenge("Coudet"))
        {
            return RedirectToAction(nameof(Index));
        }

        var attempts = Math.Max(0, intentos);
        HttpContext.Session.SetInt32("coudetAttempts", attempts);

        var diceValues = ParseDiceValues(dados);
        if (diceValues.Length != 5)
        {
            diceValues = ParseDiceValues(HttpContext.Session.GetString("coudetDice") ?? string.Empty);
        }

        var diceText = string.Join(", ", diceValues);
        HttpContext.Session.SetString("coudetDice", diceText);
        HttpContext.Session.SetString("coudetHeld", held ?? string.Empty);

        if (attempts <= 0)
        {
            TempData["mensaje"] = "Primero tenés que tirar los dados para intentar la generala.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Coudet));
        }

        var isCorrect = diceValues.All(value => value == diceValues[0]);

        if (isCorrect)
        {
            SaveProgress("Coudet");
            HttpContext.Session.Remove("coudetAttempts");
            HttpContext.Session.Remove("coudetDice");
            HttpContext.Session.Remove("coudetHeld");
            TempData["mensaje"] = "Correcto: tiraste la generala y abriste la salida de la primera sala.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }

        if (attempts >= 100)
        {
            TempData["mensaje"] = "Llegaste al intento 100 y todavía no tenías la generala. Te obligaron a repetir desde el principio.";
            TempData["correcto"] = false;
            HttpContext.Session.Remove("coudetAttempts");
            HttpContext.Session.Remove("coudetDice");
            HttpContext.Session.Remove("coudetHeld");
            return RedirectToAction(nameof(Coudet));
        }

        TempData["mensaje"] = $"Tirada {attempts}: {diceText}. Los dados todavía no coinciden. Seguí tirando.";
        TempData["correcto"] = false;
        return RedirectToAction(nameof(Coudet));
    }

    private static int[] ParseDiceValues(string dados)
    {
        if (string.IsNullOrWhiteSpace(dados))
        {
            return Array.Empty<int>();
        }

        return dados
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .Where(value => value >= 1 && value <= 6)
            .ToArray();
    }

    public IActionResult Acuna()
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        // Verificar que no se acabó el tiempo
        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Acuna";
        ViewBag.PartidaEnCurso = true;
        if (TempData["ShowIntro"] != null) ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"])) ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Acuna(string[] objetos)
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        var selected = objetos ?? Array.Empty<string>();
        var expected = new[] { "gorro", "remera", "mochila" };
        var isCorrect = selected.Length == expected.Length && expected.All(item => selected.Contains(item, StringComparer.OrdinalIgnoreCase));

        if (isCorrect)
        {
            SaveProgress("Acuna");
            TempData["mensaje"] = "Perfecto: armaste el vestuario 360° y encontraste los objetos clave.";
            TempData["correcto"] = true;
            return RedirectToAction(nameof(Demichelis));
        }
        else
        {
            TempData["mensaje"] = "Todavía falta algo en el vestuario. Revisá la lista de prendas y objetos.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Acuna));
    }

    [HttpPost]
    public IActionResult AdvanceFromAcuna(string[] objetos)
    {
        if (!CanAccessChallenge("Acuna"))
        {
            return RedirectToAction(nameof(Index));
        }

        // mark Acuna as completed and go directly to Demichelis
        SaveProgress("Acuna");
        TempData["mensaje"] = "Perfecto: armaste el vestuario 360° y encontraste los objetos clave.";
        TempData["correcto"] = true;
        return RedirectToAction(nameof(Demichelis));
    }

    public IActionResult Demichelis()
    {
        if (!CanAccessChallenge("Demichelis"))
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Demichelis";
        ViewBag.PartidaEnCurso = true;
        if (TempData["ShowIntro"] != null) ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"])) ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Demichelis(string respuesta)
    {
        if (!CanAccessChallenge("Demichelis"))
        {
            return RedirectToAction(nameof(Index));
        }

        var palabra1 = Request.Form["palabra1"].ToString();
        var palabra2 = Request.Form["palabra2"].ToString();
        var palabra3 = Request.Form["palabra3"].ToString();

        var respuestaCombinada = string.Join(" ", new[] { palabra1, palabra2, palabra3 }
            .Select(p => p?.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p)));

        if (string.IsNullOrWhiteSpace(respuestaCombinada))
        {
            respuestaCombinada = respuesta ?? string.Empty;
        }

        // Build expected phrase from the database table Demichelis
        var palabras = _bd.ObtenerPalabrasDemichelis();
        var expected = string.Join(" ", palabras)
            .Replace("\r", " ")
            .Replace("\n", " ");
        expected = string.Join(" ", expected.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

        var isCorrect = string.Equals(respuestaCombinada.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Demichelis");
            TempData["mensaje"] = "Excelente: decodificaste el mensaje del jet-lag y se abre la siguiente salida.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "El audio estaba en alemán pero la clave está en el diccionario. Releé la frase.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Demichelis));
    }

    public IActionResult Tapia()
    {
        if (!CanAccessChallenge("Tapia"))
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Tapia";
        ViewBag.PartidaEnCurso = true;
        if (TempData["ShowIntro"] != null) ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"])) ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Tapia(string respuesta)
    {
        if (!CanAccessChallenge("Tapia"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "CIRCULO-TRIANGULO-CUADRADO", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "CÍRCULO-TRIÁNGULO-CUADRADO", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Tapia");
            TempData["mensaje"] = "Bien: la figura de arriba coincide con el orden correcto de abajo.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "La secuencia visual no coincide todavía. Revisá el orden de las figuras.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Tapia));
    }

    [HttpPost]
    public IActionResult TapiaComplete()
    {
        if (!CanAccessChallenge("Tapia"))
        {
            return Json(new { redirect = Url.Action(nameof(Index)) });
        }

        SaveProgress("Tapia");
        TempData["mensaje"] = "Genial: resolviste la sala Tapia.";
        TempData["correcto"] = true;

        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var actionName = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                return Json(new { redirect = Url.Action(actionName) });
            }
        }

        return Json(new { redirect = Url.Action(nameof(Ganaste)) });
    }

    public IActionResult Scaloni()
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Scaloni";
        ViewBag.PartidaEnCurso = true;
        if (TempData["ShowIntro"] != null) ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"])) ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Scaloni(string respuesta)
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "DERECHA-IZQUIERDA-DERECHA-CENTRO", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "D-I-D-C", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "D, I, D, C", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Scaloni");
            TempData["mensaje"] = "Correcto: recordaste bien el camino y desbloqueaste la ruta.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "La secuencia no quedó grabada. Mirá otra vez el recorrido y repetilo con calma.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Scaloni));
    }

    [HttpPost]
    public IActionResult ScaloniComplete()
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return Json(new { redirect = Url.Action(nameof(Index)) });
        }

        SaveProgress("Scaloni");
        TempData["mensaje"] = "Perfecto: recordaste bien el camino y desbloqueaste la ruta.";
        TempData["correcto"] = true;

        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var actionName = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                return Json(new { redirect = Url.Action(actionName) });
            }
        }

        return Json(new { redirect = Url.Action(nameof(Ganaste)) });
    }

    public IActionResult Donofrio()
    {
        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        var jugadores = GetDonofrioJugadores();
        var plantilla = GetDonofrioPlantilla();
        var jugadoresAdivinados = jugadores
            .Where(j => plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase))
            .OrderBy(j => j.Id)
            .ToList();

        var objetivo = jugadores
            .FirstOrDefault(j => !plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase));

        var equipoCompleto = jugadoresAdivinados.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Donofrio");
            HttpContext.Session.SetString(WinKey, "true");
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Donofrio";
        ViewBag.PartidaEnCurso = true;
        if (TempData["ShowIntro"] != null) ViewBag.IntroVideo = TempData["ShowIntro"] as string;
        else if (!string.IsNullOrWhiteSpace(Request.Query["intro"])) ViewBag.IntroVideo = "/videos/entrada-carcel.mp4";
        ViewBag.Jugadores = jugadores;
        ViewBag.Plantilla = jugadoresAdivinados;
        ViewBag.Objetivo = objetivo;
        ViewBag.Tablero = BuildDonofrioBoard(jugadoresAdivinados);
        ViewBag.EquipoCompleto = equipoCompleto;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult Donofrio(string respuesta)
    {
        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        var jugadores = GetDonofrioJugadores();
        var plantilla = GetDonofrioPlantilla();

        if (string.IsNullOrWhiteSpace(respuesta))
        {
            TempData["mensaje"] = "Escribí el nombre del jugador que querés sumar a la formación.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        var respuestaNormalizada = NormalizePlayerName(respuesta);
        var jugador = jugadores.FirstOrDefault(j => NormalizePlayerName(j.Nombre) == respuestaNormalizada);

        if (jugador == null)
        {
            TempData["mensaje"] = "Ese jugador no está en la lista del equipo. Revisá la pista y probá otra vez.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        var nombreNormalizado = NormalizePlayerName(jugador.Nombre);
        if (plantilla.Contains(nombreNormalizado, StringComparer.OrdinalIgnoreCase))
        {
            TempData["mensaje"] = $"{jugador.Nombre} ya está en la formación.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(Donofrio));
        }

        plantilla.Add(nombreNormalizado);
        SetDonofrioPlantilla(plantilla);

        var equipoCompleto = plantilla.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Donofrio");
            HttpContext.Session.SetString(WinKey, "true");
            TempData["mensaje"] = "Formación completa: ya tenés armado el equipo para continuar con el escape.";
            TempData["correcto"] = true;
        }
        else
        {
            TempData["mensaje"] = $"Acertaste: {jugador.Nombre} quedó incorporado a la formación.";
            TempData["correcto"] = true;
        }

        return RedirectToAction(nameof(Donofrio));
    }

    private List<string> GetDonofrioPlantilla()
    {
        var raw = HttpContext.Session.GetString("donofrioPlantilla") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SetDonofrioPlantilla(IEnumerable<string> nombres)
    {
        var lista = nombres
            .Select(NormalizePlayerName)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HttpContext.Session.SetString("donofrioPlantilla", string.Join('|', lista));
    }

    private static List<JugadorDiCarlo> GetDonofrioJugadores()
    {
        return new List<JugadorDiCarlo>
        {
            new() { Id = 1, Nombre = "Armani", Adivinanza = "Bajo los tres palos parece gigante, con tapadas eternas en noches de gloria. En Madrid fue la muralla más imponente, ¿quién es el pulpo que entró en la historia?", Posicion = "Arquero" },
            new() { Id = 2, Nombre = "Montiel", Adivinanza = "Por el lateral derecho no pasa nadie y de penal tiene pulso helado de acero. Campeón de América y del mundo con el Millo, ¿quién puso el sello final al trofeo?", Posicion = "Defensor" },
            new() { Id = 3, Nombre = "Passarella", Adivinanza = "Defensor con gol y voz de mando, el Gran Capitán de temple de fierro. Cinta en el brazo, zurda letal, ¿quién dominaba todo el terreno?", Posicion = "Defensor" },
            new() { Id = 4, Nombre = "Maidana", Adivinanza = "Casi no habla pero deja la vida, una muralla que no conoce la piedad. Brazos firmes, fiera en cada cruce, ¿qué caudillo es el terror de la Boca?", Posicion = "Defensor" },
            new() { Id = 5, Nombre = "Acuña", Adivinanza = "Recién llegado pero con garra de campeón, un Huevo que traba en cada jugada. Por la banda izquierda va como un camión, ¿qué lateral se puso la banda cruzada?", Posicion = "Defensor" },
            new() { Id = 6, Nombre = "Enzo Pérez", Adivinanza = "Dejó la vida en el medio y fue leyenda, se puso el buzo verde lesionado y sin dudar. Atajó noventa minutos para la historia, ¿quién es el hincha que jugó en el altar?", Posicion = "Mediocampista" },
            new() { Id = 7, Nombre = "Ponzio", Adivinanza = "Comandante de mil batallas y finales, mordía en el medio sin aflojar la garra. Eterno capitán de la era más gloriosa, ¿quién es el León de la pampa?", Posicion = "Mediocampista" },
            new() { Id = 8, Nombre = "Enzo Fernandez", Adivinanza = "Surgido del club con pase y panorama, hizo cantar a todo el Monumental. De la cantera al mundo sin escalas, ¿quién la rompió antes de ir a Europa?", Posicion = "Mediocampista" },
            new() { Id = 9, Nombre = "Julian Alvarez", Adivinanza = "Picó sin parar tras cada pelota, goleador voraz, insaciable de gol. Le hizo seis a Alianza en una sola noche, ¿qué Araña nos dio tanta pasión?", Posicion = "Delantero" },
            new() { Id = 10, Nombre = "Francescoli", Adivinanza = "Elegancia pura con la banda en el pecho, un Príncipe charrúa de clase infinita. De chilena o de tiro libre un deleite, ¿quién es el ídolo que el hincha palpita?", Posicion = "Delantero" },
            new() { Id = 11, Nombre = "Falcao", Adivinanza = "Llegó de Colombia a gritar sus goles, un Tigre voraz adentro del área. Rugió en la red con cabezazos certeros, ¿quién dejó su huella con fuerza e hidalguía?", Posicion = "Delantero" }
        };
    }

    private static List<DonofrioSlot> BuildDonofrioBoard(List<JugadorDiCarlo> jugadoresAdivinados)
    {
        var slots = new[]
        {
            new DonofrioSlot { Label = "GK", Categoria = "Arquero", CssClass = "slot-gk" },
            new DonofrioSlot { Label = "LB", Categoria = "Defensor", CssClass = "slot-lb" },
            new DonofrioSlot { Label = "CB", Categoria = "Defensor", CssClass = "slot-cb-left" },
            new DonofrioSlot { Label = "CB", Categoria = "Defensor", CssClass = "slot-cb-right" },
            new DonofrioSlot { Label = "RB", Categoria = "Defensor", CssClass = "slot-rb" },
            new DonofrioSlot { Label = "CM", Categoria = "Mediocampista", CssClass = "slot-cm-left" },
            new DonofrioSlot { Label = "CM", Categoria = "Mediocampista", CssClass = "slot-cm-right" },
            new DonofrioSlot { Label = "CAM", Categoria = "Mediocampista", CssClass = "slot-cam" },
            new DonofrioSlot { Label = "LW", Categoria = "Delantero", CssClass = "slot-lw" },
            new DonofrioSlot { Label = "ST", Categoria = "Delantero", CssClass = "slot-st" },
            new DonofrioSlot { Label = "RW", Categoria = "Delantero", CssClass = "slot-rw" }
        };

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            slot.Jugador = jugadoresAdivinados
                .FirstOrDefault(j =>
                    string.Equals(j.Posicion, slot.Categoria, StringComparison.OrdinalIgnoreCase)
                    && !usados.Contains(NormalizePlayerName(j.Nombre)));

            if (slot.Jugador != null)
            {
                usados.Add(NormalizePlayerName(slot.Jugador.Nombre));
            }
        }

        return slots.ToList();
    }

    private sealed class DonofrioSlot
    {
        public string Label { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public JugadorDiCarlo? Jugador { get; set; }
    }

    public IActionResult DiCarlo()
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        if (tiempoRestante <= 0)
        {
            LimpiarSesion();
            TempData["mensajeError"] = "Se acabó el tiempo. La partida finalizo.";
            return RedirectToAction(nameof(Index));
        }

        var jugadores = _bd.ObtenerJugadoresDiCarlo();
        var plantilla = GetDiCarloPlantilla();
        var jugadoresAdivinados = jugadores
            .Where(j => plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase))
            .OrderBy(j => j.Id)
            .ToList();

        var objetivo = jugadores
            .FirstOrDefault(j => !plantilla.Contains(NormalizePlayerName(j.Nombre), StringComparer.OrdinalIgnoreCase));

        var equipoCompleto = jugadoresAdivinados.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Di Carlo");
            HttpContext.Session.SetString(WinKey, "true");
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Di Carlo";
        ViewBag.PartidaEnCurso = true;
        ViewBag.Jugadores = jugadores;
        ViewBag.Plantilla = jugadoresAdivinados;
        ViewBag.Objetivo = objetivo;
        ViewBag.Tablero = BuildDiCarloBoard(jugadoresAdivinados);
        ViewBag.EquipoCompleto = equipoCompleto;
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult DiCarlo(string respuesta)
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        var jugadores = _bd.ObtenerJugadoresDiCarlo();
        var plantilla = GetDiCarloPlantilla();

        if (string.IsNullOrWhiteSpace(respuesta))
        {
            TempData["mensaje"] = "Escribí el nombre del jugador que querés sumar a la plantilla.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(DiCarlo));
        }

        var respuestaNormalizada = NormalizePlayerName(respuesta);
        var jugador = jugadores.FirstOrDefault(j => NormalizePlayerName(j.Nombre) == respuestaNormalizada);

        if (jugador == null)
        {
            TempData["mensaje"] = "Ese jugador no está en la base de datos. Revisá el nombre y probá otra vez.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(DiCarlo));
        }

        var nombreNormalizado = NormalizePlayerName(jugador.Nombre);
        if (plantilla.Contains(nombreNormalizado, StringComparer.OrdinalIgnoreCase))
        {
            TempData["mensaje"] = $"{jugador.Nombre} ya está en la plantilla.";
            TempData["correcto"] = false;
            return RedirectToAction(nameof(DiCarlo));
        }

        plantilla.Add(nombreNormalizado);
        SetDiCarloPlantilla(plantilla);

        var equipoCompleto = plantilla.Count == jugadores.Count;
        if (equipoCompleto)
        {
            SaveProgress("Di Carlo");
            HttpContext.Session.SetString(WinKey, "true");
            TempData["mensaje"] = "Plantilla completa: ya tenés armado el equipo para continuar con el escape.";
            TempData["correcto"] = true;
        }
        else
        {
            TempData["mensaje"] = $"Acertaste: {jugador.Nombre} quedó sumado a la plantilla.";
            TempData["correcto"] = true;
        }

        return RedirectToAction(nameof(DiCarlo));
    }

    private List<string> GetDiCarloPlantilla()
    {
        var raw = HttpContext.Session.GetString("dicarloPlantilla") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SetDiCarloPlantilla(IEnumerable<string> nombres)
    {
        var lista = nombres
            .Select(NormalizePlayerName)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HttpContext.Session.SetString("dicarloPlantilla", string.Join('|', lista));
    }

    private static List<DiCarloSlot> BuildDiCarloBoard(List<JugadorDiCarlo> jugadoresAdivinados)
    {
        var slots = new[]
        {
            new DiCarloSlot { Label = "ST", Categoria = "Delantero" },
            new DiCarloSlot { Label = "ST", Categoria = "Delantero" },
            new DiCarloSlot { Label = "LM", Categoria = "Delantero" },
            new DiCarloSlot { Label = "RM", Categoria = "Delantero" },
            new DiCarloSlot { Label = "CM", Categoria = "Mediocampista" },
            new DiCarloSlot { Label = "CDM", Categoria = "Mediocampista" },
            new DiCarloSlot { Label = "LB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "RB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "CB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "CB", Categoria = "Defensor" },
            new DiCarloSlot { Label = "GK", Categoria = "Arquero" }
        };

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in slots)
        {
            slot.Jugador = jugadoresAdivinados
                .FirstOrDefault(j =>
                    string.Equals(j.Posicion, slot.Categoria, StringComparison.OrdinalIgnoreCase)
                    && !usados.Contains(NormalizePlayerName(j.Nombre)));

            if (slot.Jugador != null)
            {
                usados.Add(NormalizePlayerName(slot.Jugador.Nombre));
            }
        }

        return slots.ToList();
    }

    private static string NormalizePlayerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim();
        normalized = new string(normalized.Normalize(NormalizationForm.FormD)
            .Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark
                && !char.IsPunctuation(ch)
                && !char.IsSymbol(ch)
                && !char.IsControl(ch))
            .ToArray());

        return normalized.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
    }

    private sealed class DiCarloSlot
    {
        public string Label { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public JugadorDiCarlo? Jugador { get; set; }
    }

    public IActionResult Ganaste()
    {
        if (!IsWon())
        {
            return RedirectToAction(nameof(Index));
        }

        var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
        ViewBag.Progress = GetProgress();
        ViewBag.TiempoRestanteSegundos = tiempoRestante;
        return View();
    }

    [HttpPost]
    public IActionResult GuardarTiempo(int tiempoSegundos)
    {
        var tiempoRestante = Math.Max(0, tiempoSegundos);
        HttpContext.Session.SetInt32("tiempoRestanteSegundos", tiempoRestante);
        GuardarTiempoRestante(tiempoRestante);
        return Ok();
    }

    private List<string> GetProgress()
    {
        var value = HttpContext.Session.GetString(ProgressKey);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
    }

    private void SaveProgress(string challenge)
    {
        var progress = GetProgress();
        var exists = progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase));

        if (!exists)
        {
            progress.Add(challenge);
            var progressStr = string.Join(',', progress);
            HttpContext.Session.SetString(ProgressKey, progressStr);
            
            var nombreParticipante = GetParticipanteActual();
            var codigoActual = HttpContext.Session.GetString("codigoActual");
            var tiempoRestante = HttpContext.Session.GetInt32("tiempoRestanteSegundos") ?? 1800;
            
            // Si no hay código, generar uno nuevo
            if (string.IsNullOrWhiteSpace(codigoActual))
            {
                codigoActual = GenerarCodigoUnico();
                HttpContext.Session.SetString("codigoActual", codigoActual);
            }
            
            // Guardar o actualizar el código con el nuevo progreso y tiempo
            _bd.GuardarCodigo(codigoActual, nombreParticipante, progressStr, tiempoRestante);
            
            // Pasar el código a la siguiente vista para mostrar al usuario
            TempData["CodigoSesion"] = codigoActual;
        }
    }

    private void GuardarTiempoRestante(int tiempoSegundos)
    {
        var nombreParticipante = GetParticipanteActual();
        var codigoActual = HttpContext.Session.GetString("codigoActual");
        if (!string.IsNullOrWhiteSpace(codigoActual))
        {
            var progress = GetProgress();
            var progressStr = string.Join(',', progress);
            _bd.GuardarCodigo(codigoActual, nombreParticipante, progressStr, tiempoSegundos);
        }
    }

    private void LimpiarSesion()
    {
        HttpContext.Session.Remove("coudetAttempts");
        HttpContext.Session.Remove("coudetDice");
        HttpContext.Session.Remove("coudetHeld");
        HttpContext.Session.Remove("participante");
        HttpContext.Session.Remove(ProgressKey);
        HttpContext.Session.Remove("codigoActual");
        HttpContext.Session.Remove("tiempoRestanteSegundos");
        HttpContext.Session.Remove("donofrioPlantilla");
        HttpContext.Session.Remove("dicarloPlantilla");
    }

    private string GenerarCodigoUnico()
    {
        const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var codigo = new System.Text.StringBuilder();
        
        // Generar código de 8 caracteres
        for (int i = 0; i < 8; i++)
        {
            codigo.Append(caracteres[random.Next(caracteres.Length)]);
        }
        
        return codigo.ToString();
    }

    private bool IsWon()
    {
        return string.Equals(HttpContext.Session.GetString(WinKey), "true", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAccessChallenge(string challengeName)
    {
        var index = Array.FindIndex(ChallengeOrder, item => string.Equals(item, challengeName, StringComparison.OrdinalIgnoreCase));
        if (index <= 0)
        {
            return true;
        }

        var previousChallenge = ChallengeOrder[index - 1];
        var progress = GetProgress();
        if (progress.Any(item => string.Equals(item, previousChallenge, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var nombreParticipante = GetParticipanteActual();
        var previousSalaId = GetSalaIdForChallenge(previousChallenge);
        return _bd.ValidarAccesoSala(nombreParticipante, previousSalaId);
    }

    private int GetSalaIdForChallenge(string challengeName)
    {
        return challengeName switch
        {
            "Coudet" => 1,
            "Acuna" => 2,
            "Demichelis" => 3,
            "Tapia" => 4,
            "Scaloni" => 5,
            "Donofrio" => 6,
            "Di Carlo" => 7,
            _ => 1
        };
    }

    private IActionResult RedirectToNextChallenge()
    {
        var progress = GetProgress();

        foreach (var challenge in ChallengeOrder)
        {
            if (!progress.Any(item => string.Equals(item, challenge, StringComparison.OrdinalIgnoreCase)))
            {
                var next = challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                };

                // mark intro video for next room
                if (string.Equals(next, nameof(DiCarlo), StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ShowIntro"] = "/videos/otro-video.mp4";
                }
                else if (!string.Equals(next, nameof(Index), StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ShowIntro"] = "/videos/entrada-carcel.mp4";
                }

                return RedirectToAction(next);
            }
        }

        return RedirectToAction(nameof(Ganaste));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private void PrepareRoomView(string roomName)
    {
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = roomName;
        ViewBag.PartidaEnCurso = true;
        ViewBag.Progress = GetProgress();
    }

    private string GetParticipanteActual()
    {
        var nombre = HttpContext.Session.GetString("participante");
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "Jugador";
            HttpContext.Session.SetString("participante", nombre);
        }

        return nombre;
    }
}
