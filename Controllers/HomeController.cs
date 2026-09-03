using System.Diagnostics;
using System.Globalization;
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
    public IActionResult Continuar(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return RedirectToAction(nameof(Index));
        }

        var normalized = codigo.Trim();
        return normalized.ToUpperInvariant() switch
        {
            "CANTILO" => RedirectToAction(nameof(Coudet)),
            "VESTUARIO" => RedirectToAction(nameof(Acuna)),
            "JETLAG" => RedirectToAction(nameof(Demichelis)),
            "FIGURA" => RedirectToAction(nameof(Tapia)),
            "SECUENCIA" => RedirectToAction(nameof(Scaloni)),
            "EQUIPO" => RedirectToAction(nameof(Donofrio)),
            "RIVER" => RedirectToAction(nameof(DiCarlo)),
            _ => RedirectToAction(nameof(Index))
        };
    }

    public IActionResult Coudet()
    {
        if (!CanAccessChallenge("Coudet"))
        {
            return RedirectToAction(nameof(Index));
        }

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
        ViewBag.Attempts = attempts;
        ViewBag.Dice = dice;
        ViewBag.Held = held;
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
            diceValues = ParseDiceValues(HttpContext.Session.GetString("coudetDice"));
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

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Acuna";
        ViewBag.PartidaEnCurso = true;
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

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Demichelis";
        ViewBag.PartidaEnCurso = true;
        return View();
    }

    [HttpPost]
    public IActionResult Demichelis(string respuesta)
    {
        if (!CanAccessChallenge("Demichelis"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "LLEGAMOS AL FINAL DEL CONTENEDOR", StringComparison.OrdinalIgnoreCase);

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

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Tapia";
        ViewBag.PartidaEnCurso = true;
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

    public IActionResult Scaloni()
    {
        if (!CanAccessChallenge("Scaloni"))
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Scaloni";
        ViewBag.PartidaEnCurso = true;
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

    public IActionResult Donofrio()
    {
        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Donofrio";
        ViewBag.PartidaEnCurso = true;
        return View();
    }

    [HttpPost]
    public IActionResult Donofrio(string respuesta)
    {
        if (!CanAccessChallenge("Donofrio"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "4-3-3", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "4 3 3", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Donofrio");
            TempData["mensaje"] = "Excelente: formaste el equipo con la mejor estructura para el partido.";
            TempData["correcto"] = true;
            return RedirectToNextChallenge();
        }
        else
        {
            TempData["mensaje"] = "El once quedó desordenado. Pensá en equilibrio y en la presión del mediocampo.";
            TempData["correcto"] = false;
        }

        return RedirectToAction(nameof(Donofrio));
    }

    public IActionResult DiCarlo()
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Mensaje = TempData["mensaje"];
        ViewBag.Correcto = TempData["correcto"];
        ViewBag.Progress = GetProgress();
        ViewBag.Participante = GetParticipanteActual();
        ViewBag.HabitacionActual = "Di Carlo";
        ViewBag.PartidaEnCurso = true;
        return View();
    }

    [HttpPost]
    public IActionResult DiCarlo(string respuesta)
    {
        if (!CanAccessChallenge("Di Carlo"))
        {
            return RedirectToAction(nameof(Index));
        }

        var isCorrect = string.Equals(respuesta?.Trim(), "PRESIONAR", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "PRESIONAR Y LLEGAR AL SEGUNDO PALO", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(respuesta?.Trim(), "SEGUNDO PALO", StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            SaveProgress("Di Carlo");
            HttpContext.Session.SetString(WinKey, "true");
            TempData["mensaje"] = "Que linda decisión: el partido se ganó por la presión y la paciencia. Pezzela logró escapar.";
            TempData["correcto"] = true;
            return RedirectToAction(nameof(Ganaste));
        }

        TempData["mensaje"] = "La vida no se define por el primer pase. Elegí la opción más inteligente para sostener el partido.";
        TempData["correcto"] = false;
        return RedirectToAction(nameof(DiCarlo));
    }

    public IActionResult Ganaste()
    {
        if (!IsWon())
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Progress = GetProgress();
        return View();
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
            HttpContext.Session.SetString(ProgressKey, string.Join(',', progress));
        }
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
                return RedirectToAction(challenge switch
                {
                    "Coudet" => nameof(Coudet),
                    "Acuna" => nameof(Acuna),
                    "Demichelis" => nameof(Demichelis),
                    "Tapia" => nameof(Tapia),
                    "Scaloni" => nameof(Scaloni),
                    "Donofrio" => nameof(Donofrio),
                    "Di Carlo" => nameof(DiCarlo),
                    _ => nameof(Index)
                });
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
