# Обмер меша змея

Скрипты одноразового обмера `Assets/Models/Fury_v01.blend`. Ими получено
удлинение `WindowConfiguration.AspectRatio = 7.76`; разбор — в
`docs/kite-anatomy.md` §1.

Запуск (Blender 4.2, пути windows-овские, потому что exe виндовый):

```
"/mnt/c/Program Files/Blender Foundation/Blender 4.2/blender.exe" -b \
  "G:\work\kitesim2\Assets\Models\Fury_v01.blend" \
  -P "G:\work\kitesim2\Tools\blender\dump_fury.py"
```

- `measure_fury.py` — габариты, сумма площадей треугольников, кандидаты в размах.
- `dump_fury.py` — вершины и грани, плюс площадь планформы в проекции на
  собственную плоскость змея. Именно она и даёт удлинение: `measure_fury.py`
  делит площадь пополам, считая поверхность двусторонней, что для открытого
  паруса из четырёх треугольников неверно.
- `make_100sixteen.py` — низкополигональный proxy донора M4 из опубликованных
  чисел плана. См. `docs/donors/100sixteen.md`.
