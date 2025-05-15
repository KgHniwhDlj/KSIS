import os
import shutil
from datetime import datetime
from flask import Flask, request, send_from_directory, jsonify, abort, render_template
from werkzeug.utils import secure_filename

app = Flask(__name__)

# Корневая папка файлового хранилища
STORAGE_ROOT = os.path.join(os.getcwd(), 'storage')
if not os.path.exists(STORAGE_ROOT):
    os.makedirs(STORAGE_ROOT)


def get_abs_path(path):
    """
    Преобразует запрошенный путь в абсолютный путь в файловой системе, 
    предотвращая обход за пределы STORAGE_ROOT.
    """
    # Нормализуем путь и убираем ведущие разделители, чтобы путь был относительный
    safe_path = os.path.normpath(path).lstrip(os.path.sep)
    return os.path.join(STORAGE_ROOT, safe_path)


@app.route('/', methods=['GET'])
def index():
    """
    Отдает HTML-интерфейс для работы через браузер.
    """
    return render_template('index.html')


@app.route('/<path:subpath>', methods=['PUT', 'GET', 'HEAD', 'DELETE'])
def handle_file(subpath):
    abs_path = get_abs_path(subpath)

    # Загрузка файла (PUT)
    if request.method == 'PUT':
        existed = os.path.exists(abs_path)
        try:
            # Создаем все необходимые каталоги
            os.makedirs(os.path.dirname(abs_path), exist_ok=True)
            # Записываем данные (перезапись, если файл уже существует)
            with open(abs_path, 'wb') as f:
                f.write(request.data)
            # Если файл до запроса отсутствовал — возвращаем 201, иначе 200
            return ('', 201) if not existed else ('', 200)
        except Exception as e:
            return str(e), 500

    # Получение файла или каталога (GET)
    elif request.method == 'GET':
        if os.path.exists(abs_path):
            if os.path.isdir(abs_path):
                # Если запрошен каталог, получаем список файлов
                items = os.listdir(abs_path)
                response_list = []
                for item in items:
                    item_path = os.path.join(abs_path, item)
                    response_list.append({
                        'name': item,
                        'type': 'directory' if os.path.isdir(item_path) else 'file'
                    })
                # Если клиент ожидает HTML, отдаем простую HTML-страницу
                if 'text/html' in request.headers.get('Accept', ''):
                    html = f'<html><head><title>Содержимое каталога: {subpath}</title></head><body>'
                    html += f'<h1>Содержимое каталога: {subpath}</h1><ul>'
                    for entry in response_list:
                        html += f"<li>{entry['name']} ({entry['type']})</li>"
                    html += '</ul></body></html>'
                    return html, 200, {'Content-Type': 'text/html'}
                else:
                    # JSON-ответ со списком файлов
                    return jsonify(response_list), 200
            else:
                # Если запрошен файл — отдаём его (используем send_from_directory, который обычно сам устанавливает Content-Length)
                return send_from_directory(os.path.dirname(abs_path), os.path.basename(abs_path))
        else:
            abort(404)

    # Получение информации о файле (HEAD)abs_path
    elif request.method == 'HEAD':

        if os.path.isfile(abs_path):
            stat = os.stat(abs_path)
            headers = {
                'Content-Length': str(stat.st_size),
                'Last-Modified': datetime.fromtimestamp(stat.st_mtime).strftime('%a, %d %b %Y %H:%M:%S')
            }
            with open(abs_path, 'r', encoding='utf-8') as file:
                content = file.read()  # Весь текст файла сохраняется в переменную content

            response = app.make_response((content, 200, headers))
            response.headers.remove('Server')
            response.headers.remove('Date')
            response.headers.remove('Content-Type')
            response.headers.remove('Connection')
            return response
        else:
            abort(404)

    # Удаление файла/каталога (DELETE)
    elif request.method == 'DELETE':
        if os.path.exists(abs_path):
            try:
                if os.path.isdir(abs_path):
                    shutil.rmtree(abs_path)
                else:
                    os.remove(abs_path)
                return '', 204
            except Exception as e:
                return str(e), 500
        else:
            abort(404)
    else:
        abort(405)


# для скачивания файла (GET)
@app.route('/download/<path:subpath>', methods=['GET'])
def download_file(subpath):
    abs_path = get_abs_path(subpath)
    if os.path.exists(abs_path) and os.path.isfile(abs_path):
        # Отдаем файл как вложение для скачивания
        response = send_from_directory(os.path.dirname(abs_path), os.path.basename(abs_path), as_attachment=True)
        # Устанавливаем Content-Length по размеру файла
        file_stats = os.stat(abs_path)
        response.headers['Content-Length'] = str(file_stats.st_size)
        return response
    else:
        abort(404)


if __name__ == '__main__':
    app.run(debug=True, host='0.0.0.0')
