# Unity MCP 使用规则与最佳实践

> 本文档总结了通过 MCP (Model Context Protocol) 远程操控 Unity Editor 的完整经验，
> 包括工具用法、UGUI 创建模式、踩坑记录和可复用的工作流模板。
> 可直接迁移到任何使用 Unity MCP 的新项目中。

## 目录

1. [MCP 服务基础](#一mcp-服务基础) - 服务端配置、会话初始化
2. [可用工具清单](#二可用工具清单) - manage_gameobject、manage_components 等
3. [UGUI 创建模式](#三ugui-创建模式核心知识) - RectTransform、InputField、Button
4. [踩坑记录](#四踩坑记录与解决方案) - HTTP、参数命名、组件绑定
5. [完整工作流模板](#五完整工作流模板) - 预制体创建标准流程
6. [Bash Helper 脚本](#六bash-helper-脚本模板) - 命令行工具
7. [迁移检查清单](#七迁移到新项目的检查清单)
8. [Python 脚本调用 MCP](#八python-脚本调用-mcp-踩坑经验) - 会话初始化、常用函数
9. [预制体制作工作流](#九预制体制作工作流重要) - **核心章节**

---

## 一、MCP 服务基础

### 1.1 服务端

Unity MCP 基于 FastMCP，使用 Streamable HTTP 传输协议。

- 默认地址: `http://localhost:8080/mcp`
- 传输方式: Streamable HTTP + SSE (Server-Sent Events)
- Unity 端: 安装 MCP for Unity 插件后，在 Unity Editor 中启动 MCP Server

### 1.2 会话初始化

```bash
# 初始化会话（必须同时 Accept json 和 SSE）
RESPONSE=$(curl -s -D- -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"cli","version":"1.0"}}}')

# 从响应头提取 Session ID
SESSION_ID=$(echo "$RESPONSE" | grep -i "mcp-session-id" | tr -d '\r' | awk '{print $2}')
echo "$SESSION_ID" > /tmp/unity_mcp_session

# 发送 initialized 通知
curl -s -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SESSION_ID" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'
```

### 1.3 关键 Header 要求

| Header | 值 | 说明 |
|--------|---|------|
| Content-Type | `application/json; charset=utf-8` | 包含中文时必须加 charset |
| Accept | `application/json, text/event-stream` | **必须同时包含两者**，否则返回 406 |
| Mcp-Session-Id | `{session_id}` | 初始化后每次请求都要带 |

---

## 二、可用工具清单

### 2.1 manage_gameobject — 创建/删除/修改 GameObject

```json
{
  "action": "create",          // create | delete | modify | duplicate | move_relative | look_at
  "name": "MyObject",          // 对象名称
  "parent": "Canvas",          // 父对象名称（可选）
  "components_to_add": [       // ⚠️ 不是 "components"
    "UnityEngine.UI.Image",
    "UnityEngine.UI.Button"
  ]
}
```

有效 action（⚠️ 只有这些，没有 set_active / rename / set_parent）:
- `create` — 创建新对象
- `delete` — 删除对象（通过 target 指定）
- `modify` — 修改对象属性（包括 `is_active`、`name` 等）
- `duplicate` — 复制对象
- `move_relative` — 相对移动
- `look_at` — 朝向目标

⚠️ 设置激活状态用 `modify`，不是 `set_active`：
```json
{"action": "modify", "target": "ErrorText", "is_active": false}
```

### 2.2 manage_components — 添加组件/修改属性

```json
// 设置单个属性
{
  "target": "MyObject",
  "action": "set_property",
  "component_type": "UnityEngine.UI.Text",
  "property": "text",
  "value": "Hello"
}

// 批量设置属性
{
  "target": "MyObject",
  "action": "set_property",
  "component_type": "UnityEngine.RectTransform",
  "properties": {
    "anchorMin": {"x": 0, "y": 0},
    "anchorMax": {"x": 1, "y": 1},
    "offsetMin": {"x": 0, "y": 0},
    "offsetMax": {"x": 0, "y": 0}
  }
}

// 添加组件
{
  "target": "MyObject",
  "action": "add",
  "component_type": "UnityEngine.UI.InputField"
}
```

action 类型:
- `set_property` — 设置属性（支持单个 property+value 或批量 properties）
- `add` — 添加组件
- `remove` — 移除组件

⚠️ **组件引用绑定的关键区别**:
- `property` + `value`（单属性模式）: 只能设置简单值（字符串、数字、布尔），**无法绑定组件引用**
- `properties`（批量模式 + instanceID）: **唯一能绑定组件引用的方式**

```json
// ❌ 错误：set_prop 单属性模式无法绑定组件引用
{"target":"LoginPanel", "action":"set_property",
 "component_type":"LoginPanel", "property":"usernameInput", "value":"UsernameInput"}

// ✅ 正确：properties 批量模式 + instanceID
{"target":"LoginPanel", "action":"set_property",
 "component_type":"LoginPanel",
 "properties": {
   "usernameInput": {"instanceID": -12345},
   "passwordInput": {"instanceID": -12346}
 }}
```

获取 instanceID 的方法：
```python
# 方法1: 从 create_go 返回值中提取
r = create_go("MyChild", "MyParent", ["UnityEngine.UI.Text"])
instance_id = r["data"]["instanceID"]  # 负数，如 -2426544

# 方法2: 用 find_gameobjects 搜索
r = call_tool("find_gameobjects", {"search_term": "MyChild"})
instance_id = r["data"]["instanceIDs"][0]
```

### 2.3 manage_prefabs — 预制体操作

```json
{
  "action": "create_from_gameobject",   // ⚠️ 不是 "create" 或 "instantiate"
  "target": "LoginPanel",               // ⚠️ 用名称字符串，不是 instance ID
  "prefab_path": "Assets/Resources/Prefabs/Panels/LoginPanel.prefab"  // ⚠️ 是 "prefab_path"，不是 "path"
}
```

有效 action:
- `create_from_gameobject` — 从场景对象创建/覆盖预制体
- `get_info` — 获取预制体基本信息
- `get_hierarchy` — 获取预制体内部层级结构
- `modify_contents` — 修改预制体内容

⚠️ 没有 `instantiate` action。如果需要实例化预制体，用代码或手动操作。

⚠️ 如果目标路径已有同名预制体文件，`create_from_gameobject` 会创建带数字后缀的新文件（如 `Panel 1.prefab`），而不是覆盖。需要先从磁盘删除旧文件再保存。

### 2.4 manage_scene — 场景操作

```json
{"action": "get_hierarchy", "page_size": 50}    // 获取场景层级结构
{"action": "load", "name": "Scenes/MainScene"}  // 加载场景
{"action": "save"}                                // 保存当前场景
```

有效 action:
- `get_hierarchy` — 获取场景层级
- `load` — 加载场景（⚠️ 不是 `open`）
- `save` — 保存场景

⚠️ 场景名称格式: `Scenes/SceneName`（MCP 自动补全 `Assets/` 前缀和 `.unity` 后缀）
⚠️ 如果当前场景有未保存修改，`load` 会失败，需要先 `save`

### 2.5 find_gameobjects — 搜索对象

```json
{
  "search_term": "Canvas"      // ⚠️ 不是 "name"
}
```

### 2.6 read_console — 读取控制台日志

```json
{}   // 无参数，返回最近的控制台输出
```

---

## 三、UGUI 创建模式（核心知识）

### 3.1 关键规则：UI 元素必须先添加 Image 组件

在 Canvas 下创建 UI 对象时，如果只添加逻辑组件（如 InputField、Button），Unity 会创建普通 Transform 而非 RectTransform，导致 UI 布局失败。

**解决方案**: 创建时 `components_to_add` 数组中，`UnityEngine.UI.Image` 必须排在第一位。

```json
{
  "action": "create",
  "name": "MyButton",
  "parent": "Canvas",
  "components_to_add": [
    "UnityEngine.UI.Image",     // ← 必须第一个，触发 RectTransform 创建
    "UnityEngine.UI.Button"
  ]
}
```

### 3.2 RectTransform 锚点系统

通过 `manage_components` 设置 RectTransform 属性来控制布局：

```json
{
  "target": "MyPanel",
  "action": "set_property",
  "component_type": "UnityEngine.RectTransform",
  "properties": {
    "anchorMin": {"x": 0, "y": 0},
    "anchorMax": {"x": 1, "y": 1},
    "offsetMin": {"x": 0, "y": 0},
    "offsetMax": {"x": 0, "y": 0}
  }
}
```

常用布局预设：

| 布局 | anchorMin | anchorMax | 说明 |
|------|-----------|-----------|------|
| 全屏拉伸 | (0,0) | (1,1) | offsetMin/offsetMax 都设 (0,0) |
| 顶部拉伸 | (0,1) | (1,1) | 用 sizeDelta.y 控制高度 |
| 底部拉伸 | (0,0) | (1,0) | 用 sizeDelta.y 控制高度 |
| 居中固定 | (0.5,0.5) | (0.5,0.5) | 用 sizeDelta 控制宽高 |
| 左上角 | (0,1) | (0,1) | pivot 设 (0,1) |

### 3.3 InputField 完整创建流程

InputField 是最复杂的 UGUI 组件，需要手动创建子对象并用 instanceID 绑定引用。
⚠️ 子对象必须使用全局唯一名称（见 4.5 节）。

```
步骤1: 创建主对象（Image + InputField）
步骤2: 创建 Text 子对象（仅 Text 组件，不加 Image），用唯一名称如 "UsernameInput_Text"
步骤3: 创建 Placeholder 子对象（仅 Text 组件），用唯一名称如 "UsernameInput_PH"
步骤4: 从步骤2/3的返回值中提取 instanceID
步骤5: 用 properties 字典 + instanceID 绑定 textComponent 和 placeholder
步骤6: 设置 Placeholder 文字颜色为半透明灰色
步骤7: 如果是密码框，设置 contentType = 7 (Password)
```

Python 示例：
```python
def create_input_unique(name, parent, placeholder_text, content_type=0, y_offset=0):
    create_go(name, parent, ["UnityEngine.UI.Image", "UnityEngine.UI.InputField"])
    rt_center(name, 700, 80, y_offset)
    set_image_color(name, C_WHITE)

    text_name = f"{name}_Text"
    ph_name = f"{name}_PH"

    # 创建子对象并获取 instanceID
    r = create_go(text_name, name, ["UnityEngine.UI.Text"])
    text_id = r["data"]["instanceID"]
    rt_stretch_padding(text_name, 10, 0, 10, 0)
    set_text(text_name, "", 26, C_TEXT, 3)

    r = create_go(ph_name, name, ["UnityEngine.UI.Text"])
    ph_id = r["data"]["instanceID"]
    rt_stretch_padding(ph_name, 10, 0, 10, 0)
    set_text(ph_name, placeholder_text, 26, C_PLACEHOLDER, 3)

    # 用 instanceID 绑定（唯一正确方式）
    set_props(name, "UnityEngine.UI.InputField", {
        "textComponent": {"instanceID": text_id},
        "placeholder": {"instanceID": ph_id}
    })

    if content_type != 0:
        set_prop(name, "UnityEngine.UI.InputField", "contentType", content_type)
```

### 3.4 Button 创建流程

```
步骤1: 创建主对象（Image + Button）
步骤2: 创建 Text 子对象（Image + Text）
步骤3: 设置 Image 颜色为按钮背景色
步骤4: 设置 Text 内容和样式
```

### 3.5 常用属性值速查

**Text 对齐方式 (alignment 枚举值)**:
| 值 | 对齐 |
|----|------|
| 0 | UpperLeft |
| 1 | UpperCenter |
| 2 | UpperRight |
| 3 | MiddleLeft |
| 4 | MiddleCenter |
| 5 | MiddleRight |
| 6 | LowerLeft |
| 7 | LowerCenter |
| 8 | LowerRight |

**颜色格式**: 使用 `{r, g, b, a}` 0-1 浮点值
```json
{"r": 0.298, "g": 0.686, "b": 0.314, "a": 1.0}   // #4CAF50 绿色
{"r": 0.96, "g": 0.96, "b": 0.96, "a": 1.0}       // #F5F5F5 浅灰
{"r": 1, "g": 1, "b": 1, "a": 1}                   // 白色
{"r": 0, "g": 0, "b": 0, "a": 1}                   // 黑色
```

**InputField contentType 枚举值**:
| 值 | 类型 |
|----|------|
| 0 | Standard |
| 1 | Autocorrected |
| 2 | IntegerNumber |
| 3 | DecimalNumber |
| 6 | EmailAddress |
| 7 | Password |

---

## 四、踩坑记录与解决方案

### 4.1 HTTP 请求相关

| 问题 | 错误信息 | 解决方案 |
|------|---------|---------|
| Accept header 不完整 | `406 Not Acceptable: Client must accept both application/json and text/event-stream` | Accept 必须写 `application/json, text/event-stream` |
| 中文字符导致 JSON 解析失败 | 各种 parse error | 使用 `--data-binary @- <<< "$PAYLOAD"` 代替 `-d`，Content-Type 加 `charset=utf-8` |
| Windows 中文系统 subprocess 传中文导致 500 | `UnicodeDecodeError: 'utf-8' codec can't decode byte 0xbd` | Python 中 `json.dumps(..., ensure_ascii=True)` 将中文转义为 `\uXXXX`，彻底绕开 Windows GBK 编码问题 |
| 会话过期 | 各种连接错误 | 重新执行初始化流程获取新 Session ID |

### 4.2 工具参数命名

| 错误用法 | 正确用法 | 涉及工具 |
|---------|---------|---------|
| `components: [...]` | `components_to_add: [...]` | manage_gameobject |
| `action: "create"` | `action: "create_from_gameobject"` | manage_prefabs |
| `action: "instantiate"` | ❌ 不存在此 action | manage_prefabs |
| `action: "open"` | `action: "load"` | manage_scene |
| `action: "set_active"` | `action: "modify"` + `is_active` | manage_gameobject |
| `action: "get_properties"` | ❌ 不存在此 action | manage_components |
| `path: "xxx.prefab"` | `prefab_path: "xxx.prefab"` | manage_prefabs |
| `source_object: "xxx"` | `target: "LoginPanel"` | manage_prefabs |
| `target: "-23354"` (instance ID) | `target: "LoginPanel"` (名称) | manage_prefabs |
| `name: "Canvas"` | `search_term: "Canvas"` | find_gameobjects |
| 场景名 `"MainScene"` | `"Scenes/MainScene"` | manage_scene (load) |
| 场景名 `"Assets/Scenes/MainScene.unity"` | `"Scenes/MainScene"` | manage_scene (MCP 自动补全) |
| Layer 用整数 `5` | Layer 用字符串 `"UI"` | manage_gameobject |

### 4.3 Unity UGUI 相关

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 创建的 UI 对象没有 RectTransform | 没有先添加 UI 组件 | `components_to_add` 中 Image 放第一位 |
| InputField 无法输入文字 | 缺少 Text 子对象绑定 | 必须创建 Text 和 Placeholder 子对象并绑定引用 |
| Image 和 Text 不能共存 | 两者都继承 Graphic | Text 对象只用 `["UnityEngine.UI.Text"]`，不加 Image |
| Text 子对象的 Image 显示白色背景 | 默认 Image 颜色为白色 | 不要给 Text 对象加 Image，或设为透明 |

### 4.4 ⭐ 组件引用绑定（最大坑）

**问题**: `set_prop`（单属性 property+value 模式）无法绑定组件引用，字段始终为 None。

**原因**: MCP 的 `property`+`value` 模式只能设置简单值。组件引用必须通过 `properties` 字典 + `{"instanceID": xxx}` 格式。

**正确做法**:
```python
# 1. 获取目标对象的 instanceID
child_id = find_id("UsernameInput")  # 或从 create_go 返回值提取

# 2. 用 properties 字典批量绑定
set_props("LoginPanel", "LoginPanel", {
    "usernameInput": {"instanceID": child_id},
    "passwordInput": {"instanceID": other_id},
})
```

### 4.5 ⭐ MCP 按名称全局查找（第二大坑）

**问题**: MCP 所有操作都按名称全局查找对象。当场景中有多个同名对象时（如多个 InputField 下都有 "Text" 和 "Placeholder"），`set_text("Text", ...)` 会修改第一个找到的 "Text"，而不是当前父对象下的。

**症状**: 只有最后一个 InputField 的 placeholder 正确，其他都是空的或内容错误。

**解决方案**: 所有子对象必须使用全局唯一名称：
```python
# ❌ 错误：多个 InputField 下都有 "Text" 和 "Placeholder"
create_go("Text", "UsernameInput", ...)
create_go("Placeholder", "UsernameInput", ...)
create_go("Text", "PasswordInput", ...)       # 会和上面的 "Text" 冲突！
create_go("Placeholder", "PasswordInput", ...)

# ✅ 正确：使用唯一前缀
create_go("UsernameInput_Text", "UsernameInput", ...)
create_go("UsernameInput_PH", "UsernameInput", ...)
create_go("PasswordInput_Text", "PasswordInput", ...)
create_go("PasswordInput_PH", "PasswordInput", ...)
```

同理，Button 的 Text 子对象也要用唯一名称：
```python
# ❌ 多个按钮都有 "Text" 子对象
create_text_go("Text", "RegisterButton")
create_text_go("Text", "GoLoginButton")  # 冲突！

# ✅ 唯一名称
create_go("RegisterButton_Text", "RegisterButton", ["UnityEngine.UI.Text"])
create_go("GoLoginButton_Text", "GoLoginButton", ["UnityEngine.UI.Text"])
```

### 4.6 预制体保存命名冲突

**问题**: `create_from_gameobject` 如果目标路径已有同名 .prefab 文件，不会覆盖，而是创建 `Panel 1.prefab`、`Panel 2.prefab` 等。

**解决方案**: 保存前先从磁盘删除旧的 .prefab 和 .prefab.meta 文件：
```python
import os
panels_dir = "Assets/Resources/Prefabs/Panels"
for f in os.listdir(panels_dir):
    if f.startswith("MyPanel") and (f.endswith(".prefab") or f.endswith(".prefab.meta")):
        os.remove(os.path.join(panels_dir, f))
```

### 4.7 播放模式限制

**问题**: Unity 处于 Play Mode 时，所有 `set_property` 操作都会失败。

**错误信息**: `"This cannot be used during play mode."`

**解决方案**: 必须先在 Unity Editor 中停止播放（点击 ▶️），再执行 MCP 操作。脚本中无法通过 MCP 退出播放模式。

### 4.8 预制体编辑模式

**问题**: 如果 Unity 正在编辑某个预制体（双击预制体进入编辑模式），场景上下文会变成预制体环境。此时：
- `get_hierarchy` 返回的是预制体内部结构，不是场景
- `find_gameobjects` 找不到任何对象（返回空数组）
- `create_go` 找不到 "Canvas" 父对象
- 所有场景操作都会失败

**解决方案**: 先 `save` 当前场景，再 `load` 回目标场景：
```python
call_tool("manage_scene", {"action": "save"})
call_tool("manage_scene", {"action": "load", "name": "Scenes/MainScene"})
```

### 4.9 第三方组件绑定限制

**问题**: `{"instanceID": xxx}` 格式无法绑定第三方插件的组件类型（如 XCharts 的 BarChart、LineChart、PieChart）。

**错误信息**: `"Failed to convert value for field 'barChart' to type 'BarChart'."`

**解决方案**: 使用 C# Editor 脚本（`Assets/Editor/`）代替 MCP 来处理第三方组件绑定。Editor 脚本可以直接引用任何类型。

### 4.10 ⭐ Graphic 组件冲突（XCharts / Image）

**问题**: XCharts 的图表组件（BarChart、LineChart、PieChart 等）继承自 `Graphic`，和 `Image` 冲突——一个 GameObject 只能有一个 `Graphic` 组件。

**错误信息**: `"Can't add 'BarChart' to XXX because a 'Image' is already added to the game object! A GameObject can only contain one 'Graphic' component."`

**解决方案**: 在 Editor 脚本中添加图表组件前，先移除 `Image`：
```csharp
var img = target.GetComponent<UnityEngine.UI.Image>();
if (img != null)
    DestroyImmediate(img);
target.AddComponent<BarChart>();
```

### 4.11 ⭐ GetComponentInChildren 同类型多实例陷阱

**问题**: 当同一个 prefab 中有多个相同类型的组件（如两个 `LineChart`：一个用于折线图，一个用于体重图），`GetComponentInChildren<T>()` 总是返回第一个找到的实例，导致多个字段绑定到同一个对象。

**解决方案**: 不要用 `GetComponentInChildren<T>()` 做全局搜索，改为按 GameObject 名称精确查找：
```csharp
// ❌ 错误：两次都返回同一个 LineChart
panel.lineChart = root.GetComponentInChildren<LineChart>();
panel.weightChart = root.GetComponentInChildren<LineChart>(); // 同一个！

// ✅ 正确：按名称查找各自的 GameObject，再取组件
var lineGo = FindChildRecursive(root.transform, "LineChart");
panel.lineChart = lineGo.GetComponent<LineChart>();
var weightGo = FindChildRecursive(root.transform, "WeightChart");
panel.weightChart = weightGo.GetComponent<LineChart>();
```

### 4.12 MCP 与 Editor 脚本的分工

| 场景 | 推荐方式 | 原因 |
|------|---------|------|
| 从零创建新 prefab | MCP Python 脚本 | MCP 擅长创建 GameObject、设置属性、保存 prefab |
| 修改已有 prefab 的组件/字段 | C# Editor 脚本 | PrefabUtility API 更可靠，支持所有组件类型 |
| 绑定第三方组件引用 | C# Editor 脚本 | MCP 的 instanceID 模式不支持第三方类型 |
| 批量处理多个 prefab | C# Editor 脚本 | 一次运行处理所有，不需要逐个操作 |

Editor 脚本标准模式：
```csharp
// 放在 Assets/Editor/ 目录下，添加 [MenuItem] 菜单入口
[MenuItem("Tools/My Setup Script")]
public static void Setup()
{
    string path = "Assets/Resources/Prefabs/Panels/MyPanel.prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    // ... 修改组件、绑定字段 ...
    EditorUtility.SetDirty(component);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
}
```

---

## 五、完整工作流模板

### 5.1 预制体创建标准流程

```
1. 初始化 MCP 会话（如果尚未初始化）
2. 检查场景层级 (manage_scene → get_hierarchy)
3. 确认 Canvas 存在
4. 创建根对象 (manage_gameobject → create, parent=Canvas)
5. 设置根对象 RectTransform（全屏拉伸）
6. 设置根对象背景色
7. 逐个创建子元素：
   a. 创建对象 (manage_gameobject → create)
   b. 设置 RectTransform 布局 (manage_components → set_property)
   c. 设置组件属性（文字、颜色等）
8. 挂载脚本组件 (manage_components → add)
9. 绑定脚本字段 (manage_components → set_property)
10. 保存为预制体 (manage_prefabs → create_from_gameobject)
11. 删除场景实例 (manage_gameobject → delete)
12. 检查控制台 (read_console) 确认无错误
```

### 5.2 脚本字段绑定（⚠️ 必须用 instanceID 模式）

挂载自定义脚本后，通过 `manage_components` + `properties` 字典 + `instanceID` 绑定 public 字段：

```json
// 挂载脚本
{
  "target": "LoginPanel",
  "action": "add",
  "component_type": "LoginPanel"    // 脚本类名，不需要命名空间
}

// ❌ 错误：单属性模式无法绑定组件引用
{
  "target": "LoginPanel",
  "action": "set_property",
  "component_type": "LoginPanel",
  "property": "usernameInput",
  "value": "UsernameInput"
}

// ✅ 正确：properties 字典 + instanceID
{
  "target": "LoginPanel",
  "action": "set_property",
  "component_type": "LoginPanel",
  "properties": {
    "usernameInput": {"instanceID": -2426544},
    "passwordInput": {"instanceID": -2430008}
  }
}
```

Python helper 封装：
```python
def bind_fields(target_name, script_name, field_map):
    """field_map: {"fieldName": "childObjectName"}"""
    props = {}
    for field, child in field_map.items():
        cid = find_id(child)  # find_gameobjects 获取 instanceID
        if cid:
            props[field] = {"instanceID": cid}
    if props:
        return set_props(target_name, script_name, props)
```

### 5.3 预制体创建前的必要检查

```python
# 1. 确认不在播放模式（无法通过 MCP 检测，需用户手动停止）
# 2. 确认在正确的场景中（不在预制体编辑模式）
r = call_tool("manage_scene", {"action": "get_hierarchy", "page_size": 3})
if "MainScene" not in r.get("message", ""):
    call_tool("manage_scene", {"action": "save"})
    call_tool("manage_scene", {"action": "load", "name": "Scenes/MainScene"})

# 3. 删除磁盘上的旧预制体文件（避免命名冲突）
import os
for f in os.listdir(panels_dir):
    if f.startswith("MyPanel") and f.endswith((".prefab", ".prefab.meta")):
        os.remove(os.path.join(panels_dir, f))

# 4. 删除场景中可能残留的同名对象
delete_go("MyPanel")
```

---

## 六、Bash Helper 脚本模板

以下脚本可直接复用，保存为项目中的工具脚本：

```bash
#!/bin/bash
# unity_mcp.sh — Unity MCP 调用辅助脚本
# 用法: ./unity_mcp.sh <tool_name> '<json_args>'
# 示例: ./unity_mcp.sh manage_scene '{"action":"get_hierarchy"}'

MCP_URL="http://localhost:8080/mcp"
SESSION_FILE="/tmp/unity_mcp_session"

# 初始化函数
init_session() {
  RESPONSE=$(curl -s -D- -X POST "$MCP_URL" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"cli","version":"1.0"}}}')

  SESSION_ID=$(echo "$RESPONSE" | grep -i "mcp-session-id" | tr -d '\r' | awk '{print $2}')
  echo "$SESSION_ID" > "$SESSION_FILE"

  curl -s -X POST "$MCP_URL" \
    -H "Content-Type: application/json" \
    -H "Accept: application/json, text/event-stream" \
    -H "Mcp-Session-Id: $SESSION_ID" \
    -d '{"jsonrpc":"2.0","method":"notifications/initialized"}' > /dev/null

  echo "Session initialized: $SESSION_ID"
}

# 调用工具函数
call_tool() {
  local TOOL_NAME="$1"
  local ARGS_JSON="$2"
  local SESSION_ID=$(cat "$SESSION_FILE" 2>/dev/null)

  if [ -z "$SESSION_ID" ]; then
    echo "Error: No session. Run with 'init' first."
    return 1
  fi

  local PAYLOAD="{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"tools/call\",\"params\":{\"name\":\"$TOOL_NAME\",\"arguments\":$ARGS_JSON}}"

  curl -s -X POST "$MCP_URL" \
    -H "Content-Type: application/json; charset=utf-8" \
    -H "Accept: application/json, text/event-stream" \
    -H "Mcp-Session-Id: $SESSION_ID" \
    --data-binary @- <<< "$PAYLOAD" | \
  grep "^data:" | sed 's/^data: //' | \
  python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    r = d.get('result', d.get('error', {}))
    sc = r.get('structuredContent', None)
    if sc:
        print(json.dumps(sc, indent=2, ensure_ascii=False))
    elif 'content' in r:
        for c in r['content']:
            if c.get('type') == 'text':
                try:
                    inner = json.loads(c['text'])
                    print(json.dumps(inner, indent=2, ensure_ascii=False))
                except:
                    print(c['text'])
    else:
        print(json.dumps(r, indent=2, ensure_ascii=False))
except Exception as e:
    print(f'Parse error: {e}')
    print(sys.stdin.read() if hasattr(sys.stdin, 'read') else '')
" 2>/dev/null
}

# 主入口
case "$1" in
  init)
    init_session
    ;;
  *)
    call_tool "$1" "$2"
    ;;
esac
```

使用方式：
```bash
# 首次使用，初始化会话
./unity_mcp.sh init

# 查看场景层级
./unity_mcp.sh manage_scene '{"action":"get_hierarchy"}'

# 创建对象
./unity_mcp.sh manage_gameobject '{"action":"create","name":"MyPanel","parent":"Canvas","components_to_add":["UnityEngine.UI.Image"]}'

# 读取控制台
./unity_mcp.sh read_console '{}'
```

---

## 七、迁移到新项目的检查清单

1. 在新项目中安装 MCP for Unity 插件
2. 启动 Unity Editor 并开启 MCP Server
3. 复制本文档到新项目的 `.rules/` 目录
4. 复制 `unity_mcp.sh` 到项目根目录或工具目录
5. 运行 `./unity_mcp.sh init` 初始化会话
6. 运行 `./unity_mcp.sh manage_scene '{"action":"get_hierarchy"}'` 验证连接
7. 按照第五节的工作流模板开始创建 UI

---

## 八、Python 脚本调用 MCP 踩坑经验

### 8.1 会话初始化

```python
# 正确的会话初始化流程
def init_session():
    global SESSION_ID
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    payload = {
        "jsonrpc": "2.0", "id": 1, "method": "initialize",
        "params": {"protocolVersion": "2025-03-26", "capabilities": {}, "clientInfo": {"name": "python", "version": "1.0"}}
    }
    body, resp_headers = http_post(MCP_URL, headers, json.dumps(payload))
    
    # Session ID 可能在不同的 header key 中
    SESSION_ID = resp_headers.get("Mcp-Session-Id") or resp_headers.get("mcp-session-id")
    if not SESSION_ID:
        for key, value in resp_headers.items():
            if key.lower() == "mcp-session-id":
                SESSION_ID = value
                break
    
    # 必须发送 initialized 通知
    if SESSION_ID:
        headers["Mcp-Session-Id"] = SESSION_ID
        http_post(MCP_URL, headers, json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}))
    return SESSION_ID
```

### 8.2 场景加载

```python
# ❌ 错误：使用 path 参数
call_tool("manage_scene", {"action": "load", "path": "Assets/Scenes/MyScene.unity"})

# ✅ 正确：使用 name 参数（不含路径和扩展名）
call_tool("manage_scene", {"action": "load", "name": "MyScene"})
```

### 8.3 InputField 创建

InputField 需要手动创建 Text 和 Placeholder 子对象并绑定：

```python
def create_inputfield(name, parent, placeholder_text, is_password=False):
    # 1. 创建 InputField 容器
    input_id = create_go(name, parent, ["UnityEngine.UI.Image", "UnityEngine.UI.InputField"])
    
    # 2. 创建 Text 子对象（用于显示输入内容）
    text_name = f"{name}_Text"
    text_id = create_go(text_name, name, ["UnityEngine.UI.Text"])
    set_rt_stretch(text_name, 10, 5, 10, 5)  # 留出内边距
    set_text(text_name, "", 24, C_TEXT_PRIMARY, 3)  # 左对齐
    
    # 3. 创建 Placeholder 子对象
    placeholder_name = f"{name}_Placeholder"
    placeholder_id = create_go(placeholder_name, name, ["UnityEngine.UI.Text"])
    set_rt_stretch(placeholder_name, 10, 5, 10, 5)
    set_text(placeholder_name, placeholder_text, 24, C_TEXT_HINT, 3)
    
    # 4. 绑定 InputField 组件的引用
    props = {
        "textComponent": {"instanceID": text_id},
        "placeholder": {"instanceID": placeholder_id}
    }
    if is_password:
        props["contentType"] = 2  # Password 类型
    
    call_tool("manage_components", {
        "action": "set_property",
        "target": name,
        "component_type": "UnityEngine.UI.InputField",
        "properties": props
    })
    
    return input_id
```

### 8.4 Toggle 创建

Toggle 需要创建 Background 和 Checkmark 子对象：

```python
# 1. 创建 Toggle 对象
toggle_id = create_go("MyToggle", parent, ["UnityEngine.UI.Toggle"])

# 2. 创建背景
bg_id = create_go("MyToggle_Background", "MyToggle", ["UnityEngine.UI.Image"])
set_rt("MyToggle_Background", (0, 0.5), (0, 0.5), (0, 0.5), (30, 30), (15, 0))

# 3. 创建勾选标记（作为 Background 的子对象）
checkmark_id = create_go("MyToggle_Checkmark", "MyToggle_Background", ["UnityEngine.UI.Image"])
set_rt_stretch("MyToggle_Checkmark", 5, 5, 5, 5)
set_image("MyToggle_Checkmark", C_PRIMARY)

# 4. 绑定 Toggle 组件
call_tool("manage_components", {
    "action": "set_property",
    "target": "MyToggle",
    "component_type": "UnityEngine.UI.Toggle",
    "properties": {
        "graphic": {"instanceID": checkmark_id},      # 勾选标记
        "targetGraphic": {"instanceID": bg_id}        # 背景
    }
})
```

### 8.5 字段绑定

使用 instanceID 绑定脚本字段：

```python
def bind_fields(target, script_name, field_map):
    """绑定脚本字段
    field_map: {"fieldName": instanceID, ...}
    """
    props = {}
    for field, instance_id in field_map.items():
        if instance_id:
            props[field] = {"instanceID": instance_id}
    
    if props:
        return call_tool("manage_components", {
            "action": "set_property",
            "target": target,
            "component_type": script_name,
            "properties": props
        })
```

### 8.6 RectTransform 设置

```python
# 固定大小定位
def set_rt(target, anchor_min, anchor_max, pivot, size, pos):
    call_tool("manage_components", {
        "action": "set_property",
        "target": target,
        "component_type": "UnityEngine.RectTransform",
        "properties": {
            "anchorMin": {"x": anchor_min[0], "y": anchor_min[1]},
            "anchorMax": {"x": anchor_max[0], "y": anchor_max[1]},
            "pivot": {"x": pivot[0], "y": pivot[1]},
            "sizeDelta": {"x": size[0], "y": size[1]},
            "anchoredPosition": {"x": pos[0], "y": pos[1]}
        }
    })

# 拉伸模式（全屏或带边距）
def set_rt_stretch(target, left=0, bottom=0, right=0, top=0):
    call_tool("manage_components", {
        "action": "set_property",
        "target": target,
        "component_type": "UnityEngine.RectTransform",
        "properties": {
            "anchorMin": {"x": 0, "y": 0},
            "anchorMax": {"x": 1, "y": 1},
            "offsetMin": {"x": left, "y": bottom},
            "offsetMax": {"x": -right, "y": -top}  # 注意：right 和 top 需要取负值
        }
    })
```

### 8.7 常见颜色常量

```python
# 健康绿色主题
C_PRIMARY = {"r": 0.298, "g": 0.686, "b": 0.314, "a": 1.0}      # #4CAF50
C_BACKGROUND = {"r": 0.96, "g": 0.96, "b": 0.96, "a": 1.0}      # #F5F5F5
C_WHITE = {"r": 1, "g": 1, "b": 1, "a": 1}
C_TEXT_PRIMARY = {"r": 0.13, "g": 0.13, "b": 0.13, "a": 1}      # #212121
C_TEXT_SECONDARY = {"r": 0.46, "g": 0.46, "b": 0.46, "a": 1}    # #757575
C_TEXT_HINT = {"r": 0.7, "g": 0.7, "b": 0.7, "a": 1}            # 占位符文字
C_ERROR = {"r": 0.9, "g": 0.3, "b": 0.3, "a": 1.0}              # 错误提示
C_INPUT_BG = {"r": 0.95, "g": 0.95, "b": 0.95, "a": 1.0}        # 输入框背景
C_TRANSPARENT = {"r": 1, "g": 1, "b": 1, "a": 0}                # 透明
```

### 8.8 UI 布局踩坑

1. **标签和输入框重叠**：确保标签和输入框的 Y 坐标有足够间距（建议 50-80px）
2. **输入框背景不明显**：使用浅灰色背景 `C_INPUT_BG` 而不是纯白色
3. **Toggle 位置偏移**：Toggle 需要设置正确的锚点和 pivot
4. **Text 对齐**：alignment 值：0=UpperLeft, 3=MiddleLeft, 4=MiddleCenter, 7=LowerCenter
5. **中文编码**：使用 `json.dumps(payload, ensure_ascii=True)` 避免中文编码问题

### 8.9 预制体保存流程

```python
# 1. 创建 UI 结构
create_go("MyPanel", "Canvas", ["UnityEngine.UI.Image"])
# ... 创建子对象 ...

# 2. 添加脚本
add_script("MyPanel", "MyPanelScript")

# 3. 绑定字段
bind_fields("MyPanel", "MyPanelScript", {"field1": id1, "field2": id2})

# 4. 保存预制体
save_prefab("MyPanel", "Assets/Resources/Prefabs/Panels/MyPanel.prefab")

# 5. 删除场景实例（可选）
delete_go("MyPanel")
```

### 8.10 调试技巧

1. **打印每一步结果**：在 `create_go` 等函数中打印成功/失败信息
2. **检查 instanceID**：确保返回的 instanceID 不为 None
3. **Unity Console**：使用 `read_console` 查看 Unity 端的错误日志
4. **分步执行**：复杂 UI 分多个步骤创建，便于定位问题

---

## 九、预制体制作工作流（重要）

> ⚠️ **核心章节**：本章是预制体制作的完整指南，务必仔细阅读。

### 9.1 快速参考

| 步骤 | 操作 | 工具 |
|------|------|------|
| 1 | 停止播放模式 | `manage_editor` → `stop` |
| 2 | 切换到 PrefabScene | `manage_scene` → `load` |
| 3 | 删除旧预制体/对象 | `manage_asset` → `delete` |
| 4 | 创建 UI 结构 | Python 脚本 |
| 5 | 添加第三方组件 | C# 编辑器脚本 |
| 6 | 绑定脚本字段 | C# 编辑器脚本 |
| 7 | 保存预制体 | `manage_prefabs` → `create_from_gameobject` |
| 8 | 清理场景 | `manage_gameobject` → `delete` |

### 9.2 核心原则

1. **优先使用 Python 脚本**制作预制体，不要直接调用 MCP
2. **在 PrefabScene 中制作**，不要在业务场景中操作
3. **制作前检查**是否已存在同名预制体/对象
4. **第三方组件**（如 XCharts）必须用 C# 编辑器脚本处理

### 9.3 PrefabScene 环境要求

| 必需对象 | 组件 |
|----------|------|
| Canvas | CanvasScaler, GraphicRaycaster |
| EventSystem | StandaloneInputModule |
| Main Camera | - |
| Directional Light | - |

### 9.4 预制体制作完整流程

```python
def create_prefab_workflow(prefab_name, prefab_path, create_func):
    """预制体制作标准工作流"""
    
    # ========== 1. 切换到 PrefabScene ==========
    print(f"[1] Switching to PrefabScene...")
    call_tool("manage_scene", {"action": "load", "name": "Scenes/PrefabScene"})
    
    # ========== 2. 检查 PrefabScene 环境 ==========
    print(f"[2] Checking PrefabScene environment...")
    hierarchy = call_tool("manage_scene", {"action": "get_hierarchy"})
    if hierarchy and hierarchy.get("success"):
        items = hierarchy.get("data", {}).get("items", [])
        has_canvas = any(item["name"] == "Canvas" for item in items)
        has_eventsystem = any(item["name"] == "EventSystem" for item in items)
        
        if not has_canvas or not has_eventsystem:
            print("  ERROR: PrefabScene missing Canvas or EventSystem!")
            return False
        print("  PrefabScene environment OK")
    
    # ========== 3. 检查预制体是否已存在 ==========
    print(f"[3] Checking if prefab exists...")
    asset_result = call_tool("manage_asset", {
        "action": "search",
        "path": "Assets/Resources/Prefabs",
        "search_pattern": prefab_name,
        "filter_type": "Prefab"
    })
    
    if asset_result and asset_result.get("success"):
        assets = asset_result.get("data", {}).get("assets", [])
        if assets:
            print(f"  Prefab exists, deleting old version...")
            call_tool("manage_asset", {"action": "delete", "path": prefab_path})
    
    # ========== 4. 检查场景中是否有同名对象 ==========
    print(f"[4] Checking for existing GameObject...")
    find_result = call_tool("find_gameobjects", {
        "search_term": prefab_name,
        "search_method": "by_name"
    })
    if find_result and find_result.get("success"):
        ids = find_result.get("data", {}).get("instanceIDs", [])
        for obj_id in ids:
            print(f"  Deleting existing object: {obj_id}")
            call_tool("manage_gameobject", {
                "action": "delete",
                "target": str(obj_id),
                "search_method": "by_id"
            })
    
    # ========== 5. 创建预制体 ==========
    print(f"[5] Creating prefab...")
    create_func()  # 调用具体的创建函数
    
    # ========== 6. 保存预制体 ==========
    print(f"[6] Saving prefab...")
    save_result = call_tool("manage_prefabs", {
        "action": "create_from_gameobject",
        "target": prefab_name,
        "prefab_path": prefab_path
    })
    
    if not save_result or not save_result.get("success"):
        print(f"  ERROR: Failed to save prefab: {save_result}")
        return False
    
    # ========== 7. 验证预制体 ==========
    print(f"[7] Verifying prefab...")
    verify_result = call_tool("manage_asset", {
        "action": "get_info",
        "path": prefab_path
    })
    
    if verify_result and verify_result.get("success"):
        print(f"  Prefab verified: {prefab_path}")
    else:
        print(f"  WARNING: Could not verify prefab")
    
    # ========== 8. 清理场景 ==========
    print(f"[8] Cleaning up scene...")
    call_tool("manage_gameobject", {"action": "delete", "target": prefab_name})
    
    # ========== 9. 保存场景 ==========
    print(f"[9] Saving scene...")
    call_tool("manage_scene", {"action": "save"})
    
    print(f"[DONE] Prefab {prefab_name} created successfully!")
    return True
```

### 9.5 场景加载注意事项

```python
# ❌ 错误：直接使用场景名
call_tool("manage_scene", {"action": "load", "name": "PrefabScene"})

# ✅ 正确：使用 Scenes/场景名 格式
call_tool("manage_scene", {"action": "load", "name": "Scenes/PrefabScene"})
```

### 9.6 预制体验证检查清单

制作完成后，使用以下检查确认预制体正确：

```python
def verify_prefab(prefab_path, expected_script=None, expected_fields=None):
    """验证预制体"""
    
    # 1. 检查预制体文件存在
    result = call_tool("manage_asset", {"action": "get_info", "path": prefab_path})
    if not result or not result.get("success"):
        print(f"  ❌ Prefab file not found: {prefab_path}")
        return False
    print(f"  ✅ Prefab file exists")
    
    # 2. 获取预制体信息
    info = call_tool("manage_prefabs", {"action": "get_info", "prefab_path": prefab_path})
    if info and info.get("success"):
        data = info.get("data", {})
        print(f"  ✅ Child count: {data.get('childCount', 0)}")
        print(f"  ✅ Components: {data.get('componentTypes', [])}")
        
        # 3. 检查脚本是否挂载
        if expected_script:
            components = data.get('componentTypes', [])
            if expected_script in components:
                print(f"  ✅ Script attached: {expected_script}")
            else:
                print(f"  ❌ Script missing: {expected_script}")
                return False
    
    return True
```

### 9.7 工作流示例

```python
def main():
    init_session()
    
    # 使用标准工作流创建 MyPanel
    def create_my_panel():
        create_go("MyPanel", "Canvas", ["UnityEngine.UI.Image"])
        set_rt_stretch("MyPanel")
        # ... 创建子对象 ...
        add_script("MyPanel", "MyPanel")
        bind_fields("MyPanel", "MyPanel", {...})
    
    success = create_prefab_workflow(
        prefab_name="MyPanel",
        prefab_path="Assets/Resources/Prefabs/Panels/MyPanel.prefab",
        create_func=create_my_panel
    )
    
    if success:
        verify_prefab(
            "Assets/Resources/Prefabs/Panels/MyPanel.prefab",
            expected_script="MyPanel"
        )
```

### 9.8 常见错误及解决

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| 场景中有重复对象 | 未检查就创建 | 先用 `find_gameobjects` 检查并删除 |
| 预制体保存为 `XXX 1.prefab` | 同名预制体已存在 | 先删除旧预制体 |
| 脚本字段未绑定 | instanceID 为 None | 检查 `create_go` 返回值 |
| 场景加载失败 | 场景名格式错误 | 使用 `Scenes/场景名` 格式 |
| Canvas 找不到 | 在错误场景中操作 | 确保已切换到 PrefabScene |

### 9.9 预制体制作方式选择

**优先使用 Python 脚本制作预制体**，而不是直接调用 MCP 工具。

**原因：**
1. Python 脚本可复用、可维护、可版本控制
2. 脚本包含完整的错误处理和日志输出
3. 便于批量创建和修改预制体
4. 脚本可作为文档记录预制体结构

**MCP 直接调用适用场景：**
- 快速检查场景/预制体状态
- 简单的单次操作（如删除、重命名）
- 调试和验证

**Python 脚本制作流程：**
```python
# 1. 脚本开头：切换到 PrefabScene
call_tool("manage_scene", {"action": "load", "name": "Scenes/PrefabScene"})

# 2. 检查 PrefabScene 环境
hierarchy = call_tool("manage_scene", {"action": "get_hierarchy"})
# 验证 Canvas 和 EventSystem 存在

# 3. 检查并删除已存在的预制体和场景对象
# ... (参考 9.3 节)

# 4. 创建预制体结构
# ...

# 5. 保存预制体
call_tool("manage_prefabs", {
    "action": "create_from_gameobject",
    "target": "PanelName",
    "prefab_path": "Assets/Resources/Prefabs/Panels/PanelName.prefab"
})

# 6. 验证预制体
call_tool("manage_asset", {"action": "get_info", "path": "..."})

# 7. 清理场景并保存
call_tool("manage_gameobject", {"action": "delete", "target": "PanelName"})
call_tool("manage_scene", {"action": "save"})
```

**脚本命名规范：**
- 位置：`Assets/Editor/`
- 命名：`create_<panel_name>.py`（如 `create_diet_record_panel.py`）

### 9.10 Python 脚本调试流程

**运行脚本后如果报错，必须自行修复后重新运行，直到成功为止。**

**常见错误及解决：**

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `No module named 'requests'` | 缺少依赖 | `pip install requests` |
| `No session ID found` | 未正确初始化 | 必须先调用 `initialize` 方法 |
| `Scene has unsaved changes` | 场景未保存 | 用 MCP 保存场景后重试 |
| `Failed to load PrefabScene` | 场景名错误 | 使用 `Scenes/PrefabScene` 格式 |
| `result is None` | HTTP 请求格式错误 | 检查 SSE 响应解析逻辑 |

**正确的 MCP HTTP 调用模板：**

```python
import json
import urllib.request
import urllib.error

MCP_URL = "http://localhost:8080/mcp"
SESSION_ID = None

def http_post(url, headers, data):
    """发送 HTTP POST 请求"""
    req = urllib.request.Request(url, data=data.encode('utf-8'), headers=headers, method='POST')
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            return resp.read().decode('utf-8'), dict(resp.headers)
    except urllib.error.HTTPError as e:
        return e.read().decode('utf-8'), dict(e.headers)

def init_session():
    """初始化 MCP 会话 - 必须在调用任何工具前执行"""
    global SESSION_ID
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream"
    }
    # 1. 发送 initialize 请求
    payload = {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {
            "protocolVersion": "2025-03-26",
            "capabilities": {},
            "clientInfo": {"name": "python-prefab-creator", "version": "1.0"}
        }
    }
    body, resp_headers = http_post(MCP_URL, headers, json.dumps(payload))
    
    # 2. 获取 session ID
    SESSION_ID = resp_headers.get("Mcp-Session-Id") or resp_headers.get("mcp-session-id")
    if not SESSION_ID:
        for key, value in resp_headers.items():
            if key.lower() == "mcp-session-id":
                SESSION_ID = value
                break
    
    # 3. 发送 initialized 通知
    notify_payload = {"jsonrpc": "2.0", "method": "notifications/initialized"}
    headers["Mcp-Session-Id"] = SESSION_ID
    http_post(MCP_URL, headers, json.dumps(notify_payload))
    
    return SESSION_ID is not None

def call_tool(tool_name, args):
    """调用 MCP 工具"""
    headers = {
        "Content-Type": "application/json; charset=utf-8",
        "Accept": "application/json, text/event-stream",
        "Mcp-Session-Id": SESSION_ID
    }
    payload = {
        "jsonrpc": "2.0",
        "id": 99,
        "method": "tools/call",
        "params": {"name": tool_name, "arguments": args}
    }
    body, _ = http_post(MCP_URL, headers, json.dumps(payload, ensure_ascii=True))
    
    # 解析 SSE 格式响应
    for line in body.split('\n'):
        if line.startswith('data:'):
            try:
                data = json.loads(line[5:].strip())
                if "result" in data and "content" in data["result"]:
                    content = data["result"]["content"]
                    if content and len(content) > 0:
                        return json.loads(content[0].get("text", "{}"))
            except:
                pass
    
    # 尝试直接解析 JSON
    try:
        data = json.loads(body)
        if "result" in data and "content" in data["result"]:
            content = data["result"]["content"]
            if content and len(content) > 0:
                return json.loads(content[0].get("text", "{}"))
    except:
        pass
    
    return None
```

**脚本执行流程：**
1. 运行脚本
2. 如果报错，分析错误原因
3. 修复脚本代码
4. 重新运行
5. 重复直到成功

**注意：不要使用 MCP 直接调用来替代 Python 脚本，必须修复脚本问题。**

### 9.11 第三方插件组件绑定方案

**问题：** MCP 无法直接绑定第三方插件的组件（如 XCharts 的 BarChart、LineChart、PieChart 等），因为这些类型不在 Unity 标准命名空间中。

**解决方案：** 编写 Unity 编辑器脚本，放在 `Assets/Editor/Tools/` 目录下，由用户手动执行。

**编辑器脚本规范：**

```csharp
// 位置：Assets/Editor/Tools/BindXXXComponents.cs
using UnityEngine;
using UnityEditor;

public class BindXXXComponents
{
    [MenuItem("Tools/MCP/绑定 XXX 组件")]
    public static void Execute()
    {
        // 1. 查找预制体
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/Panels/XXXPanel.prefab");
        if (prefab == null)
        {
            Debug.LogError("找不到预制体");
            return;
        }
        
        // 2. 打开预制体编辑模式
        var prefabPath = AssetDatabase.GetAssetPath(prefab);
        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        
        // 3. 获取脚本组件
        var script = prefabRoot.GetComponent<XXXPanel>();
        if (script == null)
        {
            Debug.LogError("找不到脚本组件");
            PrefabUtility.UnloadPrefabContents(prefabRoot);
            return;
        }
        
        // 4. 查找并绑定第三方组件
        // script.barChart = prefabRoot.transform.Find("BarChart")?.GetComponent<XCharts.Runtime.BarChart>();
        
        // 5. 保存预制体
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        
        Debug.Log("✅ 组件绑定完成");
    }
}
```

**使用流程：**
1. AI 创建编辑器脚本并放在 `Assets/Editor/Tools/` 目录
2. 用户在 Unity 中点击菜单 `Tools/MCP/绑定 XXX 组件`
3. 脚本自动完成第三方组件的绑定

**命名规范：**
- 脚本文件：`Bind<功能名>Components.cs`
- 菜单路径：`Tools/MCP/<中文描述>`

**适用场景：**
- XCharts 图表组件（BarChart, LineChart, PieChart 等）
- DOTween 动画组件
- TextMeshPro 组件
- 其他第三方插件组件

### 9.12 预制体制作踩坑记录

#### 9.12.1 播放模式限制

**问题：** Python 脚本在 Unity 播放模式下无法加载场景。

**错误信息：**
```
Error loading scene: This cannot be used during play mode, please use SceneManager.LoadScene() instead.
```

**解决方案：** 运行脚本前先停止 Unity 播放模式，或在脚本中调用 `manage_editor` 的 `stop` action。

#### 9.12.2 按钮文本子对象

**问题：** 创建按钮后，脚本调用 `GetComponentInChildren<Text>()` 返回 null。

**原因：** MCP 创建的按钮默认没有 Text 子对象。

**解决方案：** 在 Python 脚本中为每个按钮创建 Text 子对象：
```python
# 创建按钮
create_go("DayButton", "Panel", ["UnityEngine.UI.Image", "UnityEngine.UI.Button"])

# 创建按钮文本子对象
create_go("DayButtonText", "DayButton", ["UnityEngine.UI.Text"])
set_rt_stretch("DayButtonText")  # 拉伸填满父对象
set_text("DayButtonText", "日", 22, 4, COLOR_WHITE)
```

#### 9.12.3 RectTransform 锚点与位置

**问题：** 使用相对锚点（如 `anchorMin: {0, 0.5}, anchorMax: {1, 0.8}`）时，元素位置在不同分辨率下不一致。

**解决方案：** 对于需要精确控制位置的元素，使用固定锚点 + sizeDelta + anchoredPosition：
```python
# 推荐：固定锚点模式
set_rt(name, 
    anchor_min={"x": 0.5, "y": 1},  # 锚点在顶部中央
    anchor_max={"x": 0.5, "y": 1},
    pivot={"x": 0.5, "y": 1},
    size_delta={"x": 400, "y": 160},  # 固定尺寸
    anchored_pos={"x": 0, "y": -260}  # 相对锚点的偏移
)
```

#### 9.12.4 第三方组件创建

**问题：** MCP 无法直接创建 XCharts 等第三方组件。

**解决方案：**
1. Python 脚本创建占位符 Image
2. 编辑器脚本删除占位符并创建真正的图表组件
3. 编辑器脚本绑定组件到脚本字段

```python
# Python 脚本中创建占位符
create_go("BarChartPlaceholder", "Panel", ["UnityEngine.UI.Image"])
```

```csharp
// 编辑器脚本中替换为真正组件
var placeholder = prefabRoot.transform.Find("BarChartPlaceholder");
if (placeholder != null) Object.DestroyImmediate(placeholder.gameObject);

var chartGo = new GameObject("BarChart");
chartGo.AddComponent<XCharts.Runtime.BarChart>();
```

#### 9.12.5 预制体字段绑定

**问题：** 通过 MCP 绑定字段时，使用 instanceID 可能不稳定。

**解决方案：** 优先使用编辑器脚本通过 `Transform.Find()` 查找并绑定：
```csharp
script.dayButton = prefabRoot.transform.Find("DayButton")?.GetComponent<Button>();
```

#### 9.12.6 场景未保存错误

**问题：** 加载场景时报错 "Scene has unsaved changes"。

**解决方案：** 在加载新场景前先保存当前场景：
```python
call_tool("manage_scene", {"action": "save"})
call_tool("manage_scene", {"action": "load", "name": "Scenes/PrefabScene"})
```

### 9.13 预制体制作完整流程总结

```
┌─────────────────────────────────────────────────────────────┐
│  1. 停止播放模式 (manage_editor → stop)                      │
├─────────────────────────────────────────────────────────────┤
│  2. 运行 Python 脚本 (python create_xxx_panel.py)           │
│     - 切换到 PrefabScene                                    │
│     - 删除旧预制体/对象                                      │
│     - 创建 UI 结构                                          │
│     - 保存预制体                                            │
├─────────────────────────────────────────────────────────────┤
│  3. 如果报错 → 修复脚本 → 重新运行                           │
├─────────────────────────────────────────────────────────────┤
│  4. 执行编辑器脚本 (Tools/MCP/创建 XXX 组件)                 │
│     - 删除占位符                                            │
│     - 创建第三方组件 (XCharts 等)                           │
├─────────────────────────────────────────────────────────────┤
│  5. 执行编辑器脚本 (Tools/MCP/绑定 XXX 字段)                 │
│     - 绑定所有脚本字段                                       │
├─────────────────────────────────────────────────────────────┤
│  6. 运行游戏测试                                            │
├─────────────────────────────────────────────────────────────┤
│  7. 如有问题 → 修复 → 重复步骤 2-6                          │
└─────────────────────────────────────────────────────────────┘
```

---

## 附录：常用命令速查

### MCP 工具速查

| 工具 | 常用 action | 示例 |
|------|-------------|------|
| `manage_editor` | `stop`, `play` | 停止/开始播放模式 |
| `manage_scene` | `load`, `save`, `get_hierarchy` | 场景操作 |
| `manage_gameobject` | `create`, `delete`, `modify` | 对象操作 |
| `manage_components` | `add`, `set_property` | 组件操作 |
| `manage_prefabs` | `create_from_gameobject`, `get_info` | 预制体操作 |
| `manage_asset` | `delete`, `get_info`, `search` | 资源操作 |
| `find_gameobjects` | - | 搜索对象 |
| `read_console` | - | 读取控制台 |

### Python 脚本模板

```python
#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""创建 XXXPanel 预制体"""

import json
import urllib.request

MCP_URL = "http://localhost:8080/mcp"
SESSION_ID = None

def init_session(): ...
def call_tool(tool_name, args): ...
def create_go(name, parent, components): ...
def set_rt(name, ...): ...
def set_text(name, text, size, align, color): ...

def main():
    init_session()
    call_tool("manage_scene", {"action": "load", "name": "Scenes/PrefabScene"})
    # ... 创建 UI ...
    call_tool("manage_prefabs", {"action": "create_from_gameobject", ...})

if __name__ == "__main__":
    main()
```

### 编辑器脚本模板

```csharp
using UnityEngine;
using UnityEditor;

public class BindXXXComponents
{
    [MenuItem("Tools/MCP/绑定 XXX 组件")]
    public static void Execute()
    {
        var prefabPath = "Assets/Resources/Prefabs/Panels/XXXPanel.prefab";
        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        
        try
        {
            var script = prefabRoot.GetComponent<XXXPanel>();
            // 绑定字段...
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            Debug.Log("✅ 完成");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}
```
