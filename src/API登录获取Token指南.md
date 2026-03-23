# API 登录获取 Token 指南

## 概述

HIS 系统的 API 接口需要 JWT Token 进行身份认证。本文档说明如何通过命令行直接获取 Token。

## 快速获取 Token

```bash
TIMESTAMP=$(date +%s%3N) && \
SIGN=$(echo -n $TIMESTAMP | base64) && \
curl -s -X POST "http://10.10.1.193:8081/api/up/Login/Login" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "appid: 201912181131469" \
  -H "deviceid: 127.321664-61561_zhszjqhtml" \
  -H "timestamp: $TIMESTAMP" \
  -H "sign: $SIGN" \
  -H "inParamEn: 0" \
  -H "outParamEn: 0" \
  -H "Authorization: " \
  -d '{"account_num":"admin","password":"Cict#S80dp","orgId":"","login_product_id":"fyylxxxt"}'
```

## 只提取 Token

结合 `jq` 只返回 token 字符串：

```bash
TIMESTAMP=$(date +%s%3N) && \
SIGN=$(echo -n $TIMESTAMP | base64) && \
curl -s -X POST "http://10.10.1.193:8081/api/up/Login/Login" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "appid: 201912181131469" \
  -H "deviceid: 127.321664-61561_zhszjqhtml" \
  -H "timestamp: $TIMESTAMP" \
  -H "sign: $SIGN" \
  -H "inParamEn: 0" \
  -H "outParamEn: 0" \
  -H "Authorization: " \
  -d '{"account_num":"admin","password":"Cict#S80dp","orgId":"","login_product_id":"fyylxxxt"}' \
  | jq -r '.data.token'
```

## 请求头说明

| 请求头 | 值 | 说明 |
|--------|-----|------|
| `Content-Type` | `application/json; charset=utf-8` | 固定值 |
| `appid` | `201912181131469` | 应用ID，固定值 |
| `deviceid` | `127.321664-61561_zhszjqhtml` | 设备标识，可自定义 |
| `timestamp` | 当前毫秒时间戳 | `date +%s%3N` 生成 |
| `sign` | Base64(timestamp) | 时间戳的 Base64 编码 |
| `inParamEn` | `0` | **关键**：设为 `0` 跳过 AES 加密，直接发明文 JSON |
| `outParamEn` | `0` | 设为 `0` 表示响应不加密 |
| `Authorization` | 空 | 登录接口无需 token |

## 请求体参数

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `account_num` | string | 是 | 登录账号 |
| `password` | string | 是 | 登录密码 |
| `orgId` | string | 否 | 机构ID，默认空 |
| `login_product_id` | string | 是 | 产品标识，固定 `fyylxxxt` |
| `fingerprint` | string | 否 | 设备指纹，可为空 |
| `login_type` | string | 否 | 登录类型，可为空 |

## 响应示例

```json
{
  "code": 200,
  "msg": "人员登录成功!",
  "data": {
    "account": "admin",
    "org_id": "1",
    "org_name": "测试机构",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "token_effective_period": 1774012475,
    "login_out_time": 120,
    "id": "1",
    "name": "**员"
  },
  "timestamp": 1774005276
}
```

## 切换科室获取指定科室的 Token

登录后的 token 不带科室信息。要获取指定科室的 token，需要调用 `ChangeStaffDepart` 接口。

### 完整两步流程

**第1步：登录获取初始 Token**

```bash
TIMESTAMP=$(date +%s%3N) && SIGN=$(echo -n $TIMESTAMP | base64) && \
TOKEN=$(curl -s -X POST "http://10.10.1.193:8081/api/up/Login/Login" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "appid: 201912181131469" \
  -H "deviceid: 127.321664-61561_zhszjqhtml" \
  -H "timestamp: $TIMESTAMP" \
  -H "sign: $SIGN" \
  -H "inParamEn: 0" -H "outParamEn: 0" -H "Authorization: " \
  -d '{"account_num":"admin","password":"Cict#S80dp","orgId":"1","login_product_id":"fyylxxxt"}' \
  | jq -r '.data.token')
```

**第2步：切换到目标科室**

```bash
TIMESTAMP=$(date +%s%3N) && SIGN=$(echo -n $TIMESTAMP | base64) && \
curl -s -X POST "http://10.10.1.193:8081/api/up/Login/ChangeStaffDepart" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "appid: 201912181131469" \
  -H "deviceid: 127.321664-61561_zhszjqhtml" \
  -H "timestamp: $TIMESTAMP" \
  -H "sign: $SIGN" \
  -H "inParamEn: 0" -H "outParamEn: 0" \
  -H "Authorization: $TOKEN" \
  -d '{"deptId":"d50134f08d3d6beb"}' | jq -r '.data.token'
```

### ChangeStaffDepart 接口说明

| 项目 | 说明 |
|------|------|
| 接口地址 | `POST api/up/Login/ChangeStaffDepart` |
| 请求体 | `{"deptId":"科室ID"}` |
| 请求头 | 需要携带登录获取的初始 token |
| 响应 | 返回新的 token，包含 `org_id`、`dept_id`、`dept_name` 等科室信息 |

### 已知科室 ID

| 科室名称 | deptId |
|---------|--------|
| 产科 | `d50134f08d3d6beb` |

> 可通过 `api/up/Login/GetStaffDeptList` 接口查询当前用户可切换的科室列表。

### 响应示例

```json
{
  "code": 200,
  "msg": "查询成功",
  "data": {
    "account": "admin",
    "org_id": "1",
    "org_name": "测试机构",
    "dept_id": "d50134f08d3d6beb",
    "dept_name": "产科",
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "token_effective_period": 1774013248
  }
}
```

## Token 使用方式

获取到 token 后，调用其他业务 API 时放在请求头中：

```bash
TIMESTAMP=$(date +%s%3N) && SIGN=$(echo -n $TIMESTAMP | base64) && \
curl -s -X POST "http://10.10.1.193:8081/api/inp/XxxController/XxxMethod" \
  -H "Content-Type: application/json; charset=utf-8" \
  -H "appid: 201912181131469" \
  -H "deviceid: 127.321664-61561_zhszjqhtml" \
  -H "timestamp: $TIMESTAMP" \
  -H "sign: $SIGN" \
  -H "inParamEn: 0" -H "outParamEn: 0" \
  -H "Authorization: $TOKEN" \
  -d '{"参数key":"参数value"}'
```

## 注意事项

- Token 有效期约 **2 小时**（`login_out_time: 120` 分钟）
- `inParamEn: 0` 是关键，设为 `1` 时请求体需要 AES-CBC 加密，否则返回 `"应用信息非法！"`
- 登录时 `orgId` 传空则 JWT 中 `orgid` 为空，传 `"1"` 则带上机构信息
- 切换科室后会返回新 token，JWT payload 中的 `orgid` 会更新
- 如果不方便用命令行，也可以在浏览器登录后从 `sessionStorage.getItem('login_data')` 中提取 token
